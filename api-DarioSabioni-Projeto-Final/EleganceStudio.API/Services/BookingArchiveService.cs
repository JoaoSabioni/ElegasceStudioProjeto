using EleganceStudio.API.Data;
using EleganceStudio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Services;

/// <summary>
/// Serviço em background que corre automaticamente à meia-noite hora de Lisboa.
/// Arquiva as marcações do dia anterior em BookingLogs e elimina-as
/// da tabela Bookings para manter a BD leve e rápida.
/// </summary>
public class BookingArchiveService : BackgroundService
{
    private static readonly TimeZoneInfo LisbonTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingArchiveService> _logger;

    public BookingArchiveService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingArchiveService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingArchiveService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilMidnightLisbon();

            _logger.LogInformation(
                "Próximo arquivo em {Delay:hh\\:mm\\:ss} (meia-noite hora de Lisboa).",
                delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await ArchiveYesterdayBookingsAsync(stoppingToken);
        }

        _logger.LogInformation("BookingArchiveService parado.");
    }

    /// <summary>
    /// Calcula quanto tempo falta para a próxima meia-noite em Lisboa.
    /// Lida correctamente com DST (horário de verão).
    /// </summary>
    private static TimeSpan GetDelayUntilMidnightLisbon()
    {
        var nowUtc      = DateTime.UtcNow;
        var nowLisbon   = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, LisbonTz);

        // Meia-noite de amanhã em hora de Lisboa
        var midnightLisbon = nowLisbon.Date.AddDays(1);

        // Converter de volta para UTC para calcular o delay correcto
        // (ConvertTimeToUtc lida com a mudança de DST se acontecer durante a noite)
        var midnightUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(midnightLisbon, DateTimeKind.Unspecified),
            LisbonTz);

        var delay = midnightUtc - nowUtc;

        // Segurança: nunca esperar menos de 1 segundo nem mais de 25 horas
        if (delay <= TimeSpan.Zero)        delay = TimeSpan.FromSeconds(1);
        if (delay > TimeSpan.FromHours(25)) delay = TimeSpan.FromHours(25);

        return delay;
    }

    private async Task ArchiveYesterdayBookingsAsync(CancellationToken ct)
    {
        _logger.LogInformation("A arquivar marcações antigas...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // "Hoje" em Lisboa — só arquiva dias anteriores a hoje
            var nowLisbon = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LisbonTz);
            var today     = DateOnly.FromDateTime(nowLisbon);

            // .IgnoreQueryFilters() para incluir soft-deleted (arquivamos tudo)
            var oldBookings = await db.Bookings
                .IgnoreQueryFilters()
                .Include(b => b.Barber)
                .Include(b => b.Service)
                .Where(b => b.BookingDate < today)
                .ToListAsync(ct);

            if (!oldBookings.Any())
            {
                _logger.LogInformation("Nenhuma marcação antiga para arquivar.");
                return;
            }

            var logs = oldBookings.Select(b => new BookingLog
            {
                OriginalBookingId      = b.Id,
                BarberId               = b.BarberId,
                BarberName             = b.Barber?.Name             ?? "—",
                ServiceName            = b.Service?.Name            ?? "—",
                ServicePrice           = b.Service?.Price           ?? 0,
                ServiceDurationMinutes = b.Service?.DurationMinutes ?? 0,
                BookingDate            = b.BookingDate,
                BookingTime            = b.BookingTime,
                ClientName             = b.ClientName,
                ClientPhone            = b.ClientPhone,
                ClientEmail            = b.ClientEmail,
                Status                 = b.IsDeleted ? BookingStatus.Deleted : b.Status,
                CreatedAt              = b.CreatedAt,
                ArchivedAt             = DateTime.UtcNow,
            }).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.BookingLogs.AddRange(logs);
            db.Bookings.RemoveRange(oldBookings);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Arquivo concluído: {Count} marcações movidas para BookingLogs.", logs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o arquivo de marcações.");
            // Não lança — o serviço continua para a próxima meia-noite
        }
    }
}
