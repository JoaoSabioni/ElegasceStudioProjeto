using EleganceStudio.API.Data;
using EleganceStudio.API.DTOs;
using EleganceStudio.API.Hubs;
using EleganceStudio.API.Interfaces;
using EleganceStudio.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Services;

public class BookingService : IBookingService
{
    private static readonly TimeZoneInfo LisbonTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly AppDbContext _db;
    private readonly ITokenStore _tokenStore;
    private readonly IEmailService _email;
    private readonly IHubContext<BookingHub> _hub;
    private readonly IConfiguration _config;
    private readonly TimeOnly _workStart;
    private readonly TimeOnly _workEnd;
    private readonly int _slotInterval;

    public BookingService(
        AppDbContext db,
        ITokenStore tokenStore,
        IEmailService email,
        IHubContext<BookingHub> hub,
        IConfiguration config)
    {
        _db = db;
        _tokenStore = tokenStore;
        _email = email;
        _hub = hub;
        _config = config;

        var workingHours = config.GetSection("WorkingHours");
        _workStart = TimeOnly.Parse(workingHours["Start"] ?? "09:00");
        _workEnd = TimeOnly.Parse(workingHours["End"] ?? "19:00");
        _slotInterval = int.Parse(workingHours["SlotIntervalMinutes"] ?? "30");
    }

    public async Task<BookingServiceResult<IEnumerable<BookingPublicDto>>> CreateAsync(BookingRequestDto dto)
    {
        var barber = await _db.Barbers.FirstOrDefaultAsync(b => b.Id == dto.BarberId && b.IsActive);
        if (barber == null)
            return NotFound("Barbeiro nao encontrado.");

        if (dto.ServiceIds == null || dto.ServiceIds.Count == 0)
            return BadRequest("Indica pelo menos um servico.");

        var distinctIds = dto.ServiceIds.Distinct().ToList();
        var services = await _db.Services
            .Where(s => distinctIds.Contains(s.Id))
            .ToListAsync();

        if (services.Count != distinctIds.Count)
            return NotFound("Um ou mais servicos nao encontrados.");

        var orderedServices = distinctIds
            .Select(id => services.First(service => service.Id == id))
            .ToList();

        var totalDuration = orderedServices.Sum(service => service.DurationMinutes);
        var validationError = ValidateRequestedDateTime(
            dto.BookingDate,
            dto.BookingTime,
            totalDuration);
        if (validationError is not null)
            return BadRequest(validationError);

        var requestedPlan = BuildRequestedPlan(dto.BookingTime, orderedServices);
        var flatRequestedSlots = requestedPlan.SelectMany(item => item.slots).ToList();

        await using var transaction = await _db.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

        try
        {
            var activeBookings = await _db.Bookings
                .FromSqlInterpolated($@"
                    SELECT * FROM ""Bookings""
                    WHERE ""BarberId"" = {dto.BarberId}
                      AND ""BookingDate"" = {dto.BookingDate}
                      AND ""Status"" != {BookingStatus.Cancelled}
                      AND ""IsDeleted"" = false
                    FOR UPDATE")
                .Include(b => b.Service)
                .ToListAsync();

            if (activeBookings.Any(existing =>
                    BookingSlotCalculator.Overlaps(
                        flatRequestedSlots,
                        existing.BookingTime,
                        existing.Service.DurationMinutes)))
            {
                await transaction.RollbackAsync();
                return Conflict("Slot indisponivel", "Um dos horarios pedidos ja esta reservado.");
            }

            var createdBookings = new List<Booking>();
            foreach (var (start, service, _) in requestedPlan)
            {
                var booking = new Booking
                {
                    ClientName = dto.ClientName.Trim(),
                    ClientPhone = dto.ClientPhone,
                    ClientEmail = dto.ClientEmail.Trim().ToLowerInvariant(),
                    BarberId = dto.BarberId,
                    ServiceId = service.Id,
                    BookingDate = dto.BookingDate,
                    BookingTime = start,
                    Status = BookingStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Bookings.Add(booking);
                createdBookings.Add(booking);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var fullBookings = await LoadBookingsAsync(createdBookings.Select(booking => booking.Id));
            var confirmationToken = await StoreConfirmationTokenAsync(fullBookings);
            await NotifyCreatedAsync(
                barber,
                dto,
                orderedServices,
                fullBookings,
                flatRequestedSlots,
                confirmationToken);

            return new BookingServiceResult<IEnumerable<BookingPublicDto>>(
                BookingServiceResultCode.Created,
                fullBookings.Select(BookingMapper.ToPublicDto));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<BookingServiceResult<IEnumerable<BookingPublicDto>>> ConfirmByTokenAsync(string token)
    {
        var bookingIdStr = await _tokenStore.GetAsync($"confirm:{token}");
        if (string.IsNullOrWhiteSpace(bookingIdStr))
            return NotFound("Link invalido ou expirado.");

        var bookingIds = ParseBookingIds(bookingIdStr);

        if (bookingIds.Count == 0)
            return NotFound("Link invalido ou expirado.");

        var bookings = await _db.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .Where(b => bookingIds.Contains(b.Id) && !b.IsDeleted)
            .OrderBy(b => b.BookingTime)
            .ToListAsync();

        if (bookings.Count == 0)
            return NotFound("Marcacao nao encontrada.");

        var updatedAt = DateTime.UtcNow;
        foreach (var booking in bookings)
        {
            booking.Status = BookingStatus.Confirmed;
            booking.UpdatedAt = updatedAt;
        }
        await _db.SaveChangesAsync();
        await _tokenStore.DeleteAsync($"confirm:{token}");

        foreach (var booking in bookings)
            await _hub.Clients
                .Group($"barber-{booking.BarberId}")
                .SendAsync("BookingUpdated", BookingMapper.ToBarberDto(booking));

        return new BookingServiceResult<IEnumerable<BookingPublicDto>>(
            BookingServiceResultCode.Ok,
            bookings.Select(BookingMapper.ToPublicDto));
    }

    public async Task<BookingServiceResult<IEnumerable<BookingPublicDto>>> HandleBarberActionAsync(
        string action,
        string token)
    {
        action = action.Trim().ToLowerInvariant();
        if (action is not ("confirmar" or "cancelar"))
            return BadRequest("Acao invalida.");

        var tokenValue = await _tokenStore.GetAsync($"barber-action:{token}");
        if (string.IsNullOrWhiteSpace(tokenValue))
            return NotFound("Link invalido ou expirado.");

        var parts = tokenValue.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0] != action)
            return NotFound("Link invalido ou expirado.");

        var bookingIds = ParseBookingIds(parts[1]);
        if (bookingIds.Count == 0)
            return NotFound("Link invalido ou expirado.");

        var bookings = await _db.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .Where(b => bookingIds.Contains(b.Id) && !b.IsDeleted)
            .OrderBy(b => b.BookingTime)
            .ToListAsync();

        if (bookings.Count == 0)
            return NotFound("Marcacao nao encontrada.");

        if (action == "confirmar" && bookings.Any(booking => booking.Status != BookingStatus.Pending))
            return Conflict("Marcacao ja processada", "Apenas marcacoes pendentes podem ser confirmadas por este link.");

        if (action == "cancelar" && bookings.All(booking => booking.Status == BookingStatus.Cancelled))
            return Conflict("Marcacao ja cancelada", "Esta marcacao ja estava cancelada.");

        var updatedAt = DateTime.UtcNow;
        foreach (var booking in bookings)
        {
            booking.Status = action == "confirmar"
                ? BookingStatus.Confirmed
                : BookingStatus.Cancelled;
            booking.UpdatedAt = updatedAt;
        }

        await _db.SaveChangesAsync();
        await _tokenStore.DeleteAsync($"barber-action:{token}");

        foreach (var booking in bookings)
        {
            await _hub.Clients
                .Group($"barber-{booking.BarberId}")
                .SendAsync("BookingUpdated", BookingMapper.ToBarberDto(booking));
        }

        if (action == "cancelar")
        {
            foreach (var booking in bookings)
            {
                var dateStr = booking.BookingDate.ToString("yyyy-MM-dd");
                var freedSlots = BookingSlotCalculator.OccupiedSlots(
                    booking.BookingTime,
                    booking.Service.DurationMinutes);

                foreach (var slot in freedSlots)
                    await _hub.Clients
                        .Group($"availability-{booking.BarberId}-{dateStr}")
                        .SendAsync("SlotAvailable", slot.ToString("HH\\:mm"));
            }
        }

        await NotifyClientAboutBarberActionAsync(action, bookings);

        return new BookingServiceResult<IEnumerable<BookingPublicDto>>(
            BookingServiceResultCode.Ok,
            bookings.Select(BookingMapper.ToPublicDto));
    }

    private static DateTime NowInLisbon() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LisbonTz);

    private string? ValidateRequestedDateTime(DateOnly date, TimeOnly time, int totalDurationMinutes)
    {
        var now = NowInLisbon();
        var today = DateOnly.FromDateTime(now);

        if (date < today) return "Nao e possivel marcar no passado.";
        if (date > today.AddDays(60)) return "Maximo 60 dias no futuro.";
        if (date == today && time <= TimeOnly.FromDateTime(now))
            return "Nao e possivel marcar num horario que ja passou.";
        if (time.Minute % _slotInterval != 0)
            return "Horario invalido. Use intervalos de 30 minutos.";
        if (time < _workStart || time.AddMinutes(totalDurationMinutes) > _workEnd)
            return "Horario fora do expediente.";

        return null;
    }

    private static List<(TimeOnly start, Service service, List<TimeOnly> slots)> BuildRequestedPlan(
        TimeOnly startTime,
        IEnumerable<Service> services)
    {
        var plan = new List<(TimeOnly start, Service service, List<TimeOnly> slots)>();
        var cursor = startTime;

        foreach (var service in services)
        {
            var slots = BookingSlotCalculator.OccupiedSlots(cursor, service.DurationMinutes);
            plan.Add((cursor, service, slots));
            cursor = cursor.AddMinutes(service.DurationMinutes);
        }

        return plan;
    }

    private static List<Guid> ParseBookingIds(string rawIds) =>
        rawIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();

    private async Task<List<Booking>> LoadBookingsAsync(IEnumerable<Guid> bookingIds)
    {
        var ids = bookingIds.ToList();
        return await _db.Bookings
            .Include(b => b.Barber)
            .Include(b => b.Service)
            .Where(b => ids.Contains(b.Id))
            .OrderBy(b => b.BookingTime)
            .ToListAsync();
    }

    private async Task<string> StoreConfirmationTokenAsync(List<Booking> bookings)
    {
        var token = Guid.NewGuid().ToString();
        await _tokenStore.SetAsync(
            $"confirm:{token}",
            string.Join(",", bookings.Select(booking => booking.Id)),
            TimeSpan.FromMinutes(15));

        return token;
    }

    private async Task<string> StoreBarberActionTokenAsync(
        List<Booking> bookings,
        string action)
    {
        var token = Guid.NewGuid().ToString();
        await _tokenStore.SetAsync(
            $"barber-action:{token}",
            $"{action}|{string.Join(",", bookings.Select(booking => booking.Id))}",
            TimeSpan.FromHours(48));

        return token;
    }

    private async Task NotifyCreatedAsync(
        Barber barber,
        BookingRequestDto dto,
        List<Service> orderedServices,
        List<Booking> fullBookings,
        List<TimeOnly> flatRequestedSlots,
        string confirmationToken)
    {
        var serviceNames = string.Join(" + ", orderedServices.Select(service => service.Name));
        var confirmBaseUrl = _config["PublicLinks:ConfirmBookingBaseUrl"] ?? "https://elegancestudio.pt/confirmar";
        var confirmLink = $"{confirmBaseUrl.TrimEnd('/')}/{confirmationToken}";
        var firstBooking = fullBookings[0];
        var dashboardBaseUrl = _config["PublicLinks:DashboardBookingBaseUrl"] ?? "http://localhost:3001/dashboard";
        var dashboardLink = $"{dashboardBaseUrl.TrimEnd('/')}" +
            $"?bookingId={firstBooking.Id}&barberId={dto.BarberId}&date={dto.BookingDate:yyyy-MM-dd}";
        var barberActionBaseUrl = _config["PublicLinks:BarberActionBaseUrl"]
            ?? "http://localhost:5134/api/bookings/barber-action";
        var barberConfirmToken = await StoreBarberActionTokenAsync(fullBookings, "confirmar");
        var barberCancelToken = await StoreBarberActionTokenAsync(fullBookings, "cancelar");
        var barberConfirmLink = $"{barberActionBaseUrl.TrimEnd('/')}/confirmar/{barberConfirmToken}";
        var barberCancelLink = $"{barberActionBaseUrl.TrimEnd('/')}/cancelar/{barberCancelToken}";

        await _email.SendAsync(
            dto.ClientEmail,
            dto.ClientName,
            "Confirme a sua marcacao - Elegance Studio",
            $"Marcacao recebida. {serviceNames}. Confirme aqui: {confirmLink} (valido 15 min)",
            $"""
            <p>Olá {dto.ClientName},</p>
            <p>Recebemos a sua marcação para <strong>{serviceNames}</strong>.</p>
            <p><a href="{confirmLink}">Confirmar marcação</a></p>
            <p>Este link é válido durante 15 minutos.</p>
            """);

        if (!string.IsNullOrWhiteSpace(barber.Email))
        {
            var servicesHtml = string.Join("<br>", orderedServices.Select(service =>
                $"{service.Name} ({service.DurationMinutes} min)"));

            await _email.SendAsync(
                barber.Email,
                barber.Name,
                $"Nova marcacao de {dto.ClientName} - Elegance Studio",
                $"""
                Nova marcacao de {dto.ClientName}.
                Cliente: {dto.ClientEmail} / {dto.ClientPhone}
                Servicos: {serviceNames}
                Data: {dto.BookingDate} as {dto.BookingTime:HH\:mm}
                Confirmar: {barberConfirmLink}
                Remarcar: {dashboardLink}
                Cancelar: {barberCancelLink}
                """,
                $"""
                <p>Nova marcacao para <strong>{barber.Name}</strong>.</p>
                <p><strong>Cliente:</strong> {dto.ClientName}<br>
                <strong>Email:</strong> {dto.ClientEmail}<br>
                <strong>Telefone:</strong> {dto.ClientPhone}</p>
                <p><strong>Servicos:</strong><br>{servicesHtml}</p>
                <p><strong>Data:</strong> {dto.BookingDate}<br>
                <strong>Hora:</strong> {dto.BookingTime:HH\:mm}</p>
                <p>
                    <a href="{barberConfirmLink}">Confirmar</a> |
                    <a href="{dashboardLink}">Remarcar no dashboard</a> |
                    <a href="{barberCancelLink}">Cancelar</a>
                </p>
                <p>Ao responder a este email, a resposta vai para o cliente.</p>
                """,
                dto.ClientEmail,
                dto.ClientName);
        }

        var dateStr = dto.BookingDate.ToString("yyyy-MM-dd");
        foreach (var booking in fullBookings)
            await _hub.Clients
                .Group($"barber-{dto.BarberId}")
                .SendAsync("NewBooking", BookingMapper.ToBarberDto(booking));

        foreach (var slot in flatRequestedSlots)
            await _hub.Clients
                .Group($"availability-{dto.BarberId}-{dateStr}")
                .SendAsync("SlotUnavailable", slot.ToString("HH\\:mm"));
    }

    private async Task NotifyClientAboutBarberActionAsync(
        string action,
        List<Booking> bookings)
    {
        var firstBooking = bookings[0];
        var serviceNames = string.Join(" + ", bookings.Select(booking => booking.Service.Name));
        var isConfirm = action == "confirmar";

        await _email.SendAsync(
            firstBooking.ClientEmail,
            firstBooking.ClientName,
            isConfirm
                ? "Marcacao confirmada - Elegance Studio"
                : "Marcacao cancelada - Elegance Studio",
            isConfirm
                ? $"A sua marcacao foi confirmada. {serviceNames} - {firstBooking.BookingDate} as {firstBooking.BookingTime:HH\\:mm}."
                : $"A sua marcacao foi cancelada. {serviceNames} - {firstBooking.BookingDate} as {firstBooking.BookingTime:HH\\:mm}.",
            isConfirm
                ? $"<p>A sua marcacao foi confirmada.</p><p><strong>{serviceNames}</strong><br>{firstBooking.BookingDate} as {firstBooking.BookingTime:HH\\:mm}</p>"
                : $"<p>A sua marcacao foi cancelada.</p><p><strong>{serviceNames}</strong><br>{firstBooking.BookingDate} as {firstBooking.BookingTime:HH\\:mm}</p>");
    }

    private static BookingServiceResult<IEnumerable<BookingPublicDto>> BadRequest(string title) =>
        new(BookingServiceResultCode.BadRequest, Title: title);

    private static BookingServiceResult<IEnumerable<BookingPublicDto>> NotFound(string title) =>
        new(BookingServiceResultCode.NotFound, Title: title);

    private static BookingServiceResult<IEnumerable<BookingPublicDto>> Conflict(string title, string detail) =>
        new(BookingServiceResultCode.Conflict, Title: title, Detail: detail);
}
