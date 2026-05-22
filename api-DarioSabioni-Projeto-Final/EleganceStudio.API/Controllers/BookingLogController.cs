using EleganceStudio.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EleganceStudio.API.Controllers;

[ApiController]
[Route("api/booking-logs")]
[Authorize(Roles = "Admin,Barber")]
public class BookingLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BookingLogsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/booking-logs?filter=7d|30d|total
    ///
    /// Admin  → vê logs de todos os barbeiros
    /// Barber → vê apenas os seus próprios logs (BarberId do JWT)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] string filter = "7d")
    {
        var role     = User.FindFirstValue(ClaimTypes.Role);
        var barberIdClaim = User.FindFirstValue("barberId");

        // Calcula data mínima consoante o filtro
        var cutoff = filter switch
        {
            "30d"  => DateTime.UtcNow.AddDays(-30),
            "total"=> DateTime.MinValue,
            _      => DateTime.UtcNow.AddDays(-7), // default: 7d
        };

        var query = _db.BookingLogs
            .Where(l => l.ArchivedAt >= cutoff);

        // Barber só vê os seus
        if (role == "Barber")
        {
            if (!Guid.TryParse(barberIdClaim, out var barberId))
                return Forbid();

            query = query.Where(l => l.BarberId == barberId);
        }

        var logs = await query
            .OrderByDescending(l => l.BookingDate)
            .ThenByDescending(l => l.BookingTime)
            .Select(l => new
            {
                l.Id,
                l.OriginalBookingId,
                l.BarberId,
                l.BarberName,
                l.ServiceName,
                l.ServicePrice,
                l.ServiceDurationMinutes,
                BookingDate = l.BookingDate.ToString("yyyy-MM-dd"),
                BookingTime = l.BookingTime.ToString("HH:mm"),
                l.ClientName,
                l.ClientPhone,
                l.ClientEmail,
                l.Status,
                l.CreatedAt,
                l.ArchivedAt,
            })
            .ToListAsync();

        return Ok(logs);
    }
}
