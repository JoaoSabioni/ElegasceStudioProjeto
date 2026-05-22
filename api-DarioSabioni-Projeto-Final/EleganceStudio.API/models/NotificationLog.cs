namespace EleganceStudio.API.Models;

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
