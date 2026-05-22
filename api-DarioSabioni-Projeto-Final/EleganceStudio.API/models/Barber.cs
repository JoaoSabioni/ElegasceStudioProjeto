namespace EleganceStudio.API.Models;

public class Barber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
