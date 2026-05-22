using EleganceStudio.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Hubs;

public class BookingHub : Hub
{
    private readonly AppDbContext _db;

    public BookingHub(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Público: clientes subscrevem actualizações de slots em tempo real.
    /// Grupo: availability-{barberId}-{date}
    /// </summary>
    public async Task WatchDay(string barberId, string date)
    {
        if (!Guid.TryParse(barberId, out var barberGuid))
            throw new HubException("barberId inválido.");

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out var dateOnly))
            throw new HubException("Formato de data inválido. Use yyyy-MM-dd.");

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")));

        if (dateOnly < today)
            throw new HubException("Não pode subscrever datas no passado.");

        if (dateOnly > today.AddDays(60))
            throw new HubException("Data demasiado no futuro (máximo 60 dias).");

        var exists = await _db.Barbers.AnyAsync(b => b.Id == barberGuid && b.IsActive);
        if (!exists)
            throw new HubException("Barbeiro não encontrado.");

        await Groups.AddToGroupAsync(Context.ConnectionId,
            $"availability-{barberId}-{date}");
    }

    /// <summary>
    /// Restrito: barbeiro/admin subscrevem a agenda em tempo real.
    /// Grupo: barber-{barberId}
    /// </summary>
    [Authorize(Roles = "Barber,Admin")]
    public async Task JoinBarberGroup(string barberId)
    {
        var tokenBarberId = Context.User?.FindFirst("barberId")?.Value;
        var isAdmin = Context.User?.IsInRole("Admin") ?? false;

        if (!isAdmin && tokenBarberId != barberId)
            throw new HubException("Não autorizado.");

        await Groups.AddToGroupAsync(Context.ConnectionId,
            $"barber-{barberId}");
    }
}
