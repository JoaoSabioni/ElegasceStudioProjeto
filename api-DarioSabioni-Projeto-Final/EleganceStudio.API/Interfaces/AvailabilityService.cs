using EleganceStudio.API.Data;
using EleganceStudio.API.Interfaces;
using EleganceStudio.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EleganceStudio.API.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly AppDbContext _db;
    private readonly TimeZoneInfo _lisbon;
    private readonly TimeOnly _workStart;
    private readonly TimeOnly _workEnd;
    private readonly int _slotInterval;

    public AvailabilityService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

        var wh = config.GetSection("WorkingHours");
        _workStart = TimeOnly.Parse(wh["Start"] ?? "09:00");
        _workEnd = TimeOnly.Parse(wh["End"] ?? "19:00");
        _slotInterval = int.Parse(wh["SlotIntervalMinutes"] ?? "30");
    }

    public async Task<List<TimeOnly>> GetAvailableSlotsAsync(
        Guid barberId, DateOnly date, Guid serviceId) =>
        await GetAvailableSlotsAsync(barberId, date, new[] { serviceId });

    public async Task<List<TimeOnly>> GetAvailableSlotsAsync(
        Guid barberId, DateOnly date, IReadOnlyCollection<Guid> serviceIds)
    {
        var barberExists = await _db.Barbers.AnyAsync(b => b.Id == barberId && b.IsActive);
        if (!barberExists) return new List<TimeOnly>();

        var distinctServiceIds = serviceIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctServiceIds.Count == 0) return new List<TimeOnly>();

        var services = await _db.Services
            .Where(s => distinctServiceIds.Contains(s.Id))
            .ToListAsync();

        if (services.Count != distinctServiceIds.Count) return new List<TimeOnly>();

        var totalDuration = distinctServiceIds
            .Select(id => services.First(s => s.Id == id).DurationMinutes)
            .Sum();

        if (totalDuration <= 0) return new List<TimeOnly>();

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _lisbon);
        var today = DateOnly.FromDateTime(nowLocal);
        if (date < today || date > today.AddDays(60)) return new List<TimeOnly>();

        var slots = new List<TimeOnly>();
        var current = _workStart;
        while (current.AddMinutes(totalDuration) <= _workEnd)
        {
            slots.Add(current);
            current = current.AddMinutes(_slotInterval);
        }

        if (date == today)
        {
            var nowTime = TimeOnly.FromDateTime(nowLocal);
            slots = slots.Where(s => s > nowTime).ToList();
        }

        var activeBookings = await _db.Bookings
            .Include(b => b.Service)
            .Where(b => b.BarberId == barberId
                     && b.BookingDate == date
                     && b.Status != BookingStatus.Cancelled)
            .ToListAsync();

        var blocked = new HashSet<TimeOnly>();
        foreach (var booking in activeBookings)
        {
            foreach (var slot in BookingSlotCalculator.OccupiedSlots(
                booking.BookingTime,
                booking.Service.DurationMinutes,
                _slotInterval))
                blocked.Add(slot);
        }

        return slots
            .Where(start => BookingSlotCalculator.OccupiedSlots(start, totalDuration, _slotInterval)
                .All(slot => !blocked.Contains(slot)))
            .ToList();
    }
}
