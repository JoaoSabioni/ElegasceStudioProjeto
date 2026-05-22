using EleganceStudio.API.Data;
using EleganceStudio.API.DTOs;
using EleganceStudio.API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availability;
    private readonly AppDbContext _db;

    public AvailabilityController(IAvailabilityService availability, AppDbContext db)
    {
        _availability = availability;
        _db = db;
    }

    /// <summary>
    /// GET /api/availability?barberId=...&date=...&serviceId=...
    /// GET /api/availability?barberId=...&date=...&serviceIds=...&serviceIds=...
    /// Devolve os slots disponiveis para um barbeiro, data e um ou varios servicos.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting("availability")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] Guid barberId,
        [FromQuery] Guid? serviceId,
        [FromQuery] List<Guid>? serviceIds,
        [FromQuery] DateOnly date)
    {
        var barberExists = await _db.Barbers.AnyAsync(b => b.Id == barberId && b.IsActive);
        if (!barberExists)
            return NotFound(new ProblemDetails
            { Status = 404, Title = "Barbeiro nao encontrado." });

        var requestedServiceIds = (serviceIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .ToList();

        if (requestedServiceIds.Count == 0 && serviceId.HasValue && serviceId.Value != Guid.Empty)
            requestedServiceIds.Add(serviceId.Value);

        requestedServiceIds = requestedServiceIds.Distinct().ToList();

        if (requestedServiceIds.Count == 0)
            return BadRequest(new ProblemDetails
            { Status = 400, Title = "Indica pelo menos um servico." });

        var services = await _db.Services
            .Where(s => requestedServiceIds.Contains(s.Id))
            .ToListAsync();

        if (services.Count != requestedServiceIds.Count)
            return NotFound(new ProblemDetails
            { Status = 404, Title = "Um ou mais servicos nao encontrados." });

        var orderedServices = requestedServiceIds
            .Select(id => services.First(s => s.Id == id))
            .ToList();

        var slots = await _availability.GetAvailableSlotsAsync(
            barberId, date, requestedServiceIds);

        var response = new AvailabilityResponseDto
        {
            Date = date,
            BarberId = barberId,
            ServiceId = requestedServiceIds.First(),
            ServiceIds = requestedServiceIds,
            ServiceDurationMinutes = orderedServices.First().DurationMinutes,
            TotalDurationMinutes = orderedServices.Sum(s => s.DurationMinutes),
            AvailableSlots = slots.Select(s => s.ToString("HH:mm")).ToList()
        };

        return Ok(response);
    }
}
