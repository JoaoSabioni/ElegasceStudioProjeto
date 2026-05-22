namespace EleganceStudio.API.Models;

/// <summary>
/// Registo histórico desnormalizado — os dados ficam intactos mesmo que
/// o barbeiro ou serviço sejam alterados/removidos no futuro.
/// Sem foreign keys intencionalmente.
/// </summary>
public class BookingLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Referência original (para auditoria, sem FK constraint)
    public Guid OriginalBookingId { get; set; }

    // Barbeiro (snapshot)
    public Guid BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;

    // Serviço (snapshot)
    public string ServiceName { get; set; } = string.Empty;
    public decimal ServicePrice { get; set; }
    public int ServiceDurationMinutes { get; set; }

    // Marcação
    public DateOnly BookingDate { get; set; }
    public TimeOnly BookingTime { get; set; }

    // Cliente
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;

    // Estado final quando foi arquivada
    public string Status { get; set; } = string.Empty;

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
