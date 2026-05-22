namespace EleganceStudio.API.DTOs;

public class AvailabilityResponseDto
{
    public DateOnly Date { get; set; }
    public Guid BarberId { get; set; }
    public Guid ServiceId { get; set; }
    public List<Guid> ServiceIds { get; set; } = new();
    public int ServiceDurationMinutes { get; set; }
    public int TotalDurationMinutes { get; set; }
    public List<string> AvailableSlots { get; set; } = new();
    // Ex: ["09:00", "09:30", "10:00"]
}
