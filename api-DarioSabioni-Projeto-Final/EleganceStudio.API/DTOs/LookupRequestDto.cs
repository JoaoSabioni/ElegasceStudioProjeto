using System.ComponentModel.DataAnnotations;

namespace EleganceStudio.API.DTOs;

public class LookupRequestDto
{
    [Required, RegularExpression(@"^[^@\s]+@[^@\s]+$", ErrorMessage = "Formato invalido. Use nome@dominio"), MaxLength(160)]
    public string Email { get; set; } = string.Empty;
}
