using EleganceStudio.API.Data;
using EleganceStudio.API.DTOs;
using EleganceStudio.API.Hubs;
using EleganceStudio.API.Interfaces;
using EleganceStudio.API.Models;
using EleganceStudio.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EleganceStudio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenStore _tokenStore;
    private readonly IEmailService _email;
    private readonly IHubContext<BookingHub> _hub;
    private readonly IBookingService _bookingService;
    private readonly TimeOnly _workStart;
    private readonly TimeOnly _workEnd;
    private readonly int _slotInterval;

    public BookingsController(
        AppDbContext db,
        ITokenStore tokenStore,
        IEmailService email,
        IHubContext<BookingHub> hub,
        IBookingService bookingService,
        IConfiguration config)
    {
        _db             = db;
        _tokenStore     = tokenStore;
        _email          = email;
        _hub            = hub;
        _bookingService = bookingService;

        var workingHours = config.GetSection("WorkingHours");
        _workStart = TimeOnly.Parse(workingHours["Start"] ?? "09:00");
        _workEnd = TimeOnly.Parse(workingHours["End"] ?? "19:00");
        _slotInterval = int.Parse(workingHours["SlotIntervalMinutes"] ?? "30");
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────

    private static BookingPublicDto ToPublicDto(Booking b) => new()
    {
        Id          = b.Id,
        BarberName  = b.Barber.Name,
        ServiceName = b.Service.Name,
        BookingDate = b.BookingDate,
        BookingTime = b.BookingTime,
        Status      = b.Status,
        ClientName  = b.ClientName
    };

    private static BookingBarberDto ToBarberDto(Booking b) => new()
    {
        Id                     = b.Id,
        BarberName             = b.Barber.Name,
        ServiceName            = b.Service.Name,
        ServiceDurationMinutes = b.Service.DurationMinutes,
        BookingDate            = b.BookingDate,
        BookingTime            = b.BookingTime,
        Status                 = b.Status,
        ClientName             = b.ClientName,
        ClientPhone            = b.ClientPhone,
        ClientEmail            = b.ClientEmail,
        CreatedAt              = b.CreatedAt,
        UpdatedAt              = b.UpdatedAt
    };

    private static readonly TimeZoneInfo LisbonTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private static DateTime NowInLisbon() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LisbonTz);

    private IActionResult ToPublicBookingsResult(
        BookingServiceResult<IEnumerable<BookingPublicDto>> result)
    {
        return result.Code switch
        {
            BookingServiceResultCode.Created => CreatedBookings(result),
            BookingServiceResultCode.Ok => OkBookings(result),
            BookingServiceResultCode.BadRequest => BadRequest(ToProblemDetails(400, result)),
            BookingServiceResultCode.NotFound => NotFound(ToProblemDetails(404, result)),
            BookingServiceResultCode.Conflict => Conflict(ToProblemDetails(409, result)),
            _ => StatusCode(500, ToProblemDetails(500, result))
        };
    }

    private IActionResult CreatedBookings(
        BookingServiceResult<IEnumerable<BookingPublicDto>> result)
    {
        var bookings = result.Value?.ToList() ?? new List<BookingPublicDto>();
        var firstBooking = bookings.FirstOrDefault();

        return firstBooking is null
            ? Created("/api/bookings", bookings)
            : CreatedAtAction(nameof(GetById), new { id = firstBooking.Id }, bookings);
    }

    private static IActionResult OkBookings(
        BookingServiceResult<IEnumerable<BookingPublicDto>> result)
    {
        var bookings = result.Value?.ToList() ?? new List<BookingPublicDto>();
        return bookings.Count == 1 ? new OkObjectResult(bookings[0]) : new OkObjectResult(bookings);
    }

    private static ProblemDetails ToProblemDetails(
        int status,
        BookingServiceResult<IEnumerable<BookingPublicDto>> result) => new()
    {
        Status = status,
        Title = result.Title,
        Detail = result.Detail
    };

    private bool IsAuthorizedForBarber(Guid barberId)
    {
        if (User.IsInRole("Admin")) return true;
        var tokenBarberId = User.FindFirstValue("barberId");
        return tokenBarberId != null && Guid.TryParse(tokenBarberId, out var tid) && tid == barberId;
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string ValueHash(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes);
    }

    private string? ValidateSchedule(DateOnly date, TimeOnly time, int durationMinutes)
    {
        var now = NowInLisbon();
        var today = DateOnly.FromDateTime(now);

        if (date < today) return "Nao e possivel marcar no passado.";
        if (date > today.AddDays(60)) return "Maximo 60 dias no futuro.";
        if (date == today && time <= TimeOnly.FromDateTime(now))
            return "Nao e possivel marcar num horario que ja passou.";
        if (time.Minute % _slotInterval != 0)
            return "Horario invalido. Use intervalos configurados.";
        if (time < _workStart || time.AddMinutes(durationMinutes) > _workEnd)
            return "Horario fora do expediente.";

        return null;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ENDPOINTS PÚBLICOS
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/bookings — Criar marcação com múltiplos serviços sequenciais
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("bookings")]
    public async Task<IActionResult> Create([FromBody] BookingRequestDto dto)
    {
        var result = await _bookingService.CreateAsync(dto);
        return ToPublicBookingsResult(result);
    }

    /// <summary>
    /// GET /api/bookings/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Global filter já exclui IsDeleted
        var booking = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        return Ok(ToPublicDto(booking));
    }

    /// <summary>
    /// POST /api/bookings/lookup/request
    /// Envia um codigo de consulta para o email indicado, se houver marcacoes.
    /// </summary>
    [HttpPost("lookup/request")]
    [EnableRateLimiting("lookup")]
    public async Task<IActionResult> RequestLookupCode([FromBody] LookupRequestDto dto)
    {
        var email = NormalizeEmail(dto.Email);
        var emailHash = ValueHash(email);

        var hasBookings = await _db.Bookings
            .AnyAsync(b => b.ClientEmail == email && b.Status != BookingStatus.Cancelled);

        if (hasBookings)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            await _tokenStore.SetAsync(
                $"lookup:{emailHash}:{code}",
                email,
                TimeSpan.FromMinutes(10));

            await _email.SendAsync(
                email,
                "Cliente",
                "Codigo para consultar marcacoes - Elegance Studio",
                $"Codigo para consultar as suas marcacoes: {code} (valido 10 min)",
                $"<p>O seu codigo para consultar marcacoes e <strong>{code}</strong>.</p><p>Valido durante 10 minutos.</p>");
        }

        return Accepted(new { message = "Se existirem marcacoes para este email, sera enviado um codigo." });
    }

    /// <summary>
    /// GET /api/bookings/lookup?email=...&code=...
    /// </summary>
    [HttpGet("lookup")]
    [EnableRateLimiting("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string email, [FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return BadRequest(new ProblemDetails
            { Status = 400, Title = "Email e codigo sao obrigatorios." });

        var normalizedEmail = NormalizeEmail(email);
        var normalizedCode = code.Trim();
        if (normalizedCode.Length != 6 || !normalizedCode.All(char.IsDigit))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Codigo invalido ou expirado." });

        var lookupKey = $"lookup:{ValueHash(normalizedEmail)}:{normalizedCode}";
        var storedEmail = await _tokenStore.GetAsync(lookupKey);
        if (storedEmail != normalizedEmail)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Codigo invalido ou expirado." });

        await _tokenStore.DeleteAsync(lookupKey);

        var bookings = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .Where(b => b.ClientEmail == normalizedEmail && b.Status != BookingStatus.Cancelled)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.BookingTime)
            .ToListAsync();

        return Ok(bookings.Select(ToPublicDto));
    }

    /// <summary>
    /// GET /api/bookings/confirm/{token}
    /// </summary>
    [HttpGet("confirm/{token}")]
    public async Task<IActionResult> ConfirmByToken(string token)
    {
        var result = await _bookingService.ConfirmByTokenAsync(token);
        return ToPublicBookingsResult(result);
    }

    /// <summary>
    /// GET /api/bookings/barber-action/{action}/{token}
    /// Link rapido enviado por email ao barbeiro para confirmar/cancelar.
    /// </summary>
    [HttpGet("barber-action/{action}/{token}")]
    public async Task<IActionResult> HandleBarberAction(string action, string token)
    {
        var result = await _bookingService.HandleBarberActionAsync(action, token);
        return ToPublicBookingsResult(result);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ENDPOINTS AUTENTICADOS
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/bookings/barber/{barberId}
    /// </summary>
    [HttpGet("barber/{barberId}")]
    [Authorize(Roles = "Barber,Admin")]
    public async Task<IActionResult> GetByBarber(Guid barberId)
    {
        if (!IsAuthorizedForBarber(barberId)) return Forbid();

        // Global filter já exclui IsDeleted
        var bookings = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .Where(b => b.BarberId == barberId)
            .OrderByDescending(b => b.BookingDate)
            .ThenBy(b => b.BookingTime)
            .ToListAsync();

        return Ok(bookings.Select(ToBarberDto));
    }

    /// <summary>
    /// GET /api/bookings/barber/{barberId}/day/{date}
    /// </summary>
    [HttpGet("barber/{barberId}/day/{date}")]
    [Authorize(Roles = "Barber,Admin")]
    public async Task<IActionResult> GetByBarberDay(Guid barberId, DateOnly date)
    {
        if (!IsAuthorizedForBarber(barberId)) return Forbid();

        var bookings = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .Where(b => b.BarberId == barberId && b.BookingDate == date)
            .OrderBy(b => b.BookingTime)
            .ToListAsync();

        return Ok(bookings.Select(ToBarberDto));
    }

    /// <summary>
    /// PUT /api/bookings/{id}/confirm — Barbeiro confirma
    /// </summary>
    [HttpPut("{id}/confirm")]
    [Authorize(Roles = "Barber,Admin")]
    public async Task<IActionResult> ConfirmByBarber(Guid id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        if (!IsAuthorizedForBarber(booking.BarberId)) return Forbid();

        booking.Status    = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _email.SendAsync(
            booking.ClientEmail,
            booking.ClientName,
            "Marcacao confirmada - Elegance Studio",
            $"A sua marcacao foi confirmada! {booking.Service.Name} - {booking.BookingDate} as {booking.BookingTime:HH\\:mm}.",
            $"<p>A sua marcação foi confirmada.</p><p><strong>{booking.Service.Name}</strong><br>{booking.BookingDate} às {booking.BookingTime:HH\\:mm}</p>");

        await _hub.Clients
            .Group($"barber-{booking.BarberId}")
            .SendAsync("BookingUpdated", ToBarberDto(booking));

        return Ok(ToBarberDto(booking));
    }

    /// <summary>
    /// PUT /api/bookings/{id}/cancel — Barbeiro cancela
    /// </summary>
    [HttpPut("{id}/cancel")]
    [Authorize(Roles = "Barber,Admin")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        if (!IsAuthorizedForBarber(booking.BarberId)) return Forbid();

        booking.Status    = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _email.SendAsync(
            booking.ClientEmail,
            booking.ClientName,
            "Marcacao cancelada - Elegance Studio",
            $"A sua marcacao de {booking.BookingDate} as {booking.BookingTime:HH\\:mm} foi cancelada. Contacte-nos para reagendar.",
            $"<p>A sua marcação de <strong>{booking.BookingDate} às {booking.BookingTime:HH\\:mm}</strong> foi cancelada.</p><p>Contacte-nos para reagendar.</p>");

        var dateStr    = booking.BookingDate.ToString("yyyy-MM-dd");
        var freedSlots = BookingSlotCalculator.OccupiedSlots(booking.BookingTime, booking.Service.DurationMinutes);
        foreach (var slot in freedSlots)
            await _hub.Clients
                .Group($"availability-{booking.BarberId}-{dateStr}")
                .SendAsync("SlotAvailable", slot.ToString("HH\\:mm"));

        await _hub.Clients
            .Group($"barber-{booking.BarberId}")
            .SendAsync("BookingUpdated", ToBarberDto(booking));

        return Ok(ToBarberDto(booking));
    }

    /// <summary>
    /// PUT /api/bookings/{id} — Editar marcação
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Barber,Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BookingUpdateDto dto)
    {
        var booking = await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        if (!IsAuthorizedForBarber(booking.BarberId)) return Forbid();

        var newDate      = dto.BookingDate ?? booking.BookingDate;
        var newTime      = dto.BookingTime ?? booking.BookingTime;
        var newServiceId = dto.ServiceId   ?? booking.ServiceId;

        var newService = await _db.Services.FindAsync(newServiceId);
        if (newService == null)
            return NotFound(new ProblemDetails
                { Status = 404, Title = "Serviço não encontrado." });

        var scheduleError = ValidateSchedule(newDate, newTime, newService.DurationMinutes);
        if (scheduleError is not null)
            return BadRequest(new ProblemDetails { Status = 400, Title = scheduleError });

        await using var transaction = await _db.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

        try
        {
            var activeBookings = await _db.Bookings
                .FromSqlInterpolated($@"
                    SELECT * FROM ""Bookings""
                    WHERE ""BarberId"" = {booking.BarberId}
                      AND ""BookingDate"" = {newDate}
                      AND ""Status"" != {BookingStatus.Cancelled}
                      AND ""IsDeleted"" = false
                      AND ""Id"" != {id}
                    FOR UPDATE")
                .Include(b => b.Service)
                .ToListAsync();

            var requestedSlots = BookingSlotCalculator.OccupiedSlots(newTime, newService.DurationMinutes);

            foreach (var existing in activeBookings)
            {
                if (BookingSlotCalculator.Overlaps(
                    requestedSlots,
                    existing.BookingTime,
                    existing.Service.DurationMinutes))
                {
                    await transaction.RollbackAsync();
                    return Conflict(new ProblemDetails
                    {
                        Status = 409,
                        Title  = "Slot indisponível",
                        Detail = "O novo horário já está ocupado."
                    });
                }
            }

            var oldDate  = booking.BookingDate;
            var oldSlots = BookingSlotCalculator.OccupiedSlots(booking.BookingTime, booking.Service.DurationMinutes);

            booking.BookingDate = newDate;
            booking.BookingTime = newTime;
            booking.ServiceId   = newServiceId;
            booking.UpdatedAt   = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var oldDateStr = oldDate.ToString("yyyy-MM-dd");
            foreach (var slot in oldSlots)
                await _hub.Clients
                    .Group($"availability-{booking.BarberId}-{oldDateStr}")
                    .SendAsync("SlotAvailable", slot.ToString("HH\\:mm"));

            var newDateStr = newDate.ToString("yyyy-MM-dd");
            foreach (var slot in requestedSlots)
                await _hub.Clients
                    .Group($"availability-{booking.BarberId}-{newDateStr}")
                    .SendAsync("SlotUnavailable", slot.ToString("HH\\:mm"));

            var updated = await _db.Bookings
                .Include(b => b.Barber)
                .Include(b => b.Service)
                .FirstAsync(b => b.Id == id);

            await _hub.Clients
                .Group($"barber-{booking.BarberId}")
                .SendAsync("BookingUpdated", ToBarberDto(updated));

            return Ok(ToBarberDto(updated));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ENDPOINTS ADMIN
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/bookings?date=YYYY-MM-DD — Todas as marcações (Admin)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] string? date = null)
    {
        // Global filter já exclui IsDeleted
        var query = _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate))
            query = query.Where(b => b.BookingDate == parsedDate);

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ThenBy(b => b.BookingTime)
            .ToListAsync();

        return Ok(bookings.Select(ToBarberDto));
    }

    /// <summary>
    /// DELETE /api/bookings/{id} — Soft delete (Admin ou Barbeiro dono)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Barber,Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        // .IgnoreQueryFilters() para conseguir encontrar mesmo que já esteja deleted
        var booking = await _db.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null || booking.IsDeleted) return NotFound();
        if (!IsAuthorizedForBarber(booking.BarberId)) return Forbid();

        booking.IsDeleted = true;
        booking.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dateStr = booking.BookingDate.ToString("yyyy-MM-dd");
        var freedSlots = BookingSlotCalculator.OccupiedSlots(booking.BookingTime, booking.Service.DurationMinutes);
        foreach (var slot in freedSlots)
            await _hub.Clients
                .Group($"availability-{booking.BarberId}-{dateStr}")
                .SendAsync("SlotAvailable", slot.ToString("HH\\:mm"));

        await _hub.Clients
            .Group($"barber-{booking.BarberId}")
            .SendAsync("BookingDeleted", booking.Id);

        return NoContent();
    }
}
