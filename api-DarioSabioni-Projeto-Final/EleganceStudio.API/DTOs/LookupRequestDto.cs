using System.ComponentModel.DataAnnotations;

namespace EleganceStudio.API.DTOs;

public class LookupRequestDto
{
    [Required, EmailAddress, MaxLength(160)]
    public string Email { get; set; } = string.Empty;
}
