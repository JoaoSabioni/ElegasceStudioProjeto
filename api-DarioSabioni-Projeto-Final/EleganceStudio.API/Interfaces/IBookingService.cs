using EleganceStudio.API.DTOs;

namespace EleganceStudio.API.Interfaces;

public interface IBookingService
{
    Task<BookingServiceResult<IEnumerable<BookingPublicDto>>> CreateAsync(BookingRequestDto dto);
    Task<BookingServiceResult<IEnumerable<BookingPublicDto>>> ConfirmByTokenAsync(string token);
    Task<BookingServiceResult<IEnumerable<BookingPublicDto>>> HandleBarberActionAsync(
        string action,
        string token);
}

public enum BookingServiceResultCode
{
    Created,
    Ok,
    BadRequest,
    NotFound,
    Conflict
}

public sealed record BookingServiceResult<T>(
    BookingServiceResultCode Code,
    T? Value = default,
    string? Title = null,
    string? Detail = null);
