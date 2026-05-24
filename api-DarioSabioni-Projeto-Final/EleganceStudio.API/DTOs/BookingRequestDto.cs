using System.ComponentModel.DataAnnotations;

namespace EleganceStudio.API.DTOs;

public class BookingRequestDto
{
    [Required]
    public Guid BarberId { get; set; }

    // Múltiplos serviços — mínimo 1, sem repetições (validado no controller)
    [Required, MinLength(1, ErrorMessage = "Indica pelo menos um serviço.")]
    public List<Guid> ServiceIds { get; set; } = new();

    [Required]
    public DateOnly BookingDate { get; set; }

    [Required]
    public TimeOnly BookingTime { get; set; }

    [Required, MaxLength(100)]
    public string ClientName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+351\s?\d{3}\s?\d{3}\s?\d{3}$|^\+351\d{9}$", ErrorMessage = "Formato inválido. Use +351XXXXXXXXX")]
    public string ClientPhone { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[^@\s]+@[^@\s]+$", ErrorMessage = "Formato invalido. Use nome@dominio"), MaxLength(160)]
    public string ClientEmail { get; set; } = string.Empty;
}
