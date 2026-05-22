namespace EleganceStudio.API.Interfaces;

public interface IAvailabilityService
{
    Task<List<TimeOnly>> GetAvailableSlotsAsync(
        Guid barberId, DateOnly date, Guid serviceId);

    Task<List<TimeOnly>> GetAvailableSlotsAsync(
        Guid barberId, DateOnly date, IReadOnlyCollection<Guid> serviceIds);
}
