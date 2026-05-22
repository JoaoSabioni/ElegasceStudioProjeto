using EleganceStudio.API.Data;
using EleganceStudio.API.Interfaces;
using EleganceStudio.API.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(railwayPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");

ValidateStartupConfiguration(builder.Configuration, builder.Environment);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddOpenApi();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(
            Path.Combine(builder.Environment.ContentRootPath, ".keys")));
}

// PostgreSQL + EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
var useInMemoryTokenStore = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Redis:UseInMemoryFallback");

if (useInMemoryTokenStore)
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
}
else
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = builder.Configuration["Redis:ConnectionString"]);
    builder.Services.AddSingleton<ITokenStore, RedisTokenStore>();
}

// JWT
var jwtKey = builder.Configuration["Jwt:SecretKey"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // Necessário para SignalR (token via query string)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// CORS (cliente + dashboard + SignalR)
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray()
    ?? new[]
    {
        "http://localhost:3000",
        "http://localhost:3001",
        "https://try-barbearia.vercel.app"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontends", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// SignalR
builder.Services.AddSignalR();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("bookings",     o => { o.PermitLimit = 10; o.Window = TimeSpan.FromMinutes(1); o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; o.QueueLimit = 0; });
    options.AddFixedWindowLimiter("availability", o => { o.PermitLimit = 30; o.Window = TimeSpan.FromMinutes(1); o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; o.QueueLimit = 0; });
    options.AddFixedWindowLimiter("lookup",       o => { o.PermitLimit = 5;  o.Window = TimeSpan.FromMinutes(1); o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; o.QueueLimit = 0; });
    options.AddFixedWindowLimiter("login",        o => { o.PermitLimit = 5;  o.Window = TimeSpan.FromMinutes(1); o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; o.QueueLimit = 0; });
});

// Services
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddHttpClient<IEmailService, BrevoEmailService>();
builder.Services.AddHttpClient();

// ─── Arquivo automático à meia-noite ─────────────────────────────────────────
builder.Services.AddHostedService<BookingArchiveService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseCors("AllowFrontends");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "EleganceStudio.API",
    time = DateTime.UtcNow
}));
app.MapControllers();
app.MapHub<EleganceStudio.API.Hubs.BookingHub>("/hubs/bookings");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db, app.Configuration, app.Environment);
}

app.Run();

static void ValidateStartupConfiguration(IConfiguration config, IWebHostEnvironment environment)
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection e obrigatoria.");

    var usesInMemoryTokenStore = environment.IsDevelopment()
        && config.GetValue<bool>("Redis:UseInMemoryFallback");

    var redisConnection = config["Redis:ConnectionString"];
    if (!usesInMemoryTokenStore && string.IsNullOrWhiteSpace(redisConnection))
        throw new InvalidOperationException("Redis:ConnectionString e obrigatoria.");

    var jwtSecret = config["Jwt:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
        throw new InvalidOperationException("Jwt:SecretKey e obrigatoria.");

    if (!environment.IsDevelopment() && jwtSecret.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey deve ter pelo menos 32 caracteres em producao.");

    var issuer = config["Jwt:Issuer"];
    var audience = config["Jwt:Audience"];
    if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Jwt:Issuer e Jwt:Audience sao obrigatorios.");

    if (!environment.IsDevelopment())
    {
        var brevoApiKey = config["Email:BrevoApiKey"];
        var senderEmail = config["Email:SenderEmail"];
        if (string.IsNullOrWhiteSpace(brevoApiKey) || string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException("Email:BrevoApiKey e Email:SenderEmail sao obrigatorios em producao.");
    }
}
