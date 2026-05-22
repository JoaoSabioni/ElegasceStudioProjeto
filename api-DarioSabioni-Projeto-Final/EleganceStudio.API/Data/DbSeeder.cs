using EleganceStudio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IConfiguration config,
        IWebHostEnvironment environment)
    {
        var applyMigrations = config.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
            ?? environment.IsDevelopment();

        if (applyMigrations)
            await db.Database.MigrateAsync();

        if (!await db.Barbers.AnyAsync())
        {
            var barbers = new List<Barber>
            {
                new Barber { Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001"), Name = "Edi",   Phone = "+351910000001", Email = "t82704366@gmail.com", IsActive = true },
                new Barber { Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002"), Name = "Tomas", Phone = "+351910000002", Email = "t82704366@gmail.com", IsActive = true },
                new Barber { Id = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000003"), Name = "Abreu", Phone = "+351910000003", Email = "t82704366@gmail.com", IsActive = true }
            };
            var services = new List<Service>
            {
                new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000001"), Name = "Corte Simples",      Price = 10, DurationMinutes = 30 },
                new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000002"), Name = "Corte + Barba",      Price = 15, DurationMinutes = 45 },
                new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000003"), Name = "Barba",              Price = 8,  DurationMinutes = 20 },
                new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000004"), Name = "Corte Infantil",     Price = 8,  DurationMinutes = 25 },
                new Service { Id = Guid.Parse("b2b2b2b2-0000-0000-0000-000000000005"), Name = "Tratamento Capilar", Price = 20, DurationMinutes = 60 }
            };
            await db.Barbers.AddRangeAsync(barbers);
            await db.Services.AddRangeAsync(services);
        }

        if (!await db.Users.AnyAsync())
        {
            var seedUsers = config
                .GetSection("Seed:Users")
                .Get<List<SeedUserOptions>>() ?? new List<SeedUserOptions>();

            var users = BuildUsers(seedUsers, environment.IsDevelopment());
            if (users.Count > 0)
                await db.Users.AddRangeAsync(users);
        }

        await ApplyBarberNotificationEmailsAsync(db, config);

        await db.SaveChangesAsync();
    }

    private static async Task ApplyBarberNotificationEmailsAsync(
        AppDbContext db,
        IConfiguration config)
    {
        var seedBarbers = config
            .GetSection("Seed:Barbers")
            .Get<List<SeedBarberOptions>>() ?? new List<SeedBarberOptions>();

        foreach (var seedBarber in seedBarbers)
        {
            if (seedBarber.Id is null || string.IsNullOrWhiteSpace(seedBarber.Email))
                continue;

            var barber = await db.Barbers.FindAsync(seedBarber.Id.Value);
            if (barber is null)
                continue;

            barber.Email = seedBarber.Email.Trim().ToLowerInvariant();
        }
    }

    private static List<User> BuildUsers(
        IEnumerable<SeedUserOptions> seedUsers,
        bool allowWeakPasswords)
    {
        var users = new List<User>();
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedUser in seedUsers)
        {
            var username = seedUser.Username?.Trim();
            var password = seedUser.Password?.Trim();
            var role = seedUser.Role?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(role) ||
                !usernames.Add(username))
                continue;

            if (!allowWeakPasswords && password.Length < 12)
                continue;

            if (role is not ("Admin" or "Barber"))
                continue;

            if (role == "Barber" && seedUser.BarberId is null)
                continue;

            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = username.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                BarberId = role == "Barber" ? seedUser.BarberId : null
            });
        }

        return users;
    }

    private sealed class SeedUserOptions
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
        public Guid? BarberId { get; set; }
    }

    private sealed class SeedBarberOptions
    {
        public Guid? Id { get; set; }
        public string? Email { get; set; }
    }
}
