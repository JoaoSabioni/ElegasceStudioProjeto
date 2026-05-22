using EleganceStudio.API.DTOs;
using EleganceStudio.API.Models;

namespace EleganceStudio.API.Services;

public static class BookingMapper
{
    public static BookingPublicDto ToPublicDto(Booking booking) => new()
    {
        Id = booking.Id,
        BarberName = booking.Barber.Name,
        ServiceName = booking.Service.Name,
        BookingDate = booking.BookingDate,
        BookingTime = booking.BookingTime,
        Status = booking.Status,
        ClientName = booking.ClientName
    };

    public static BookingBarberDto ToBarberDto(Booking booking) => new()
    {
        Id = booking.Id,
        BarberName = booking.Barber.Name,
        ServiceName = booking.Service.Name,
        ServiceDurationMinutes = booking.Service.DurationMinutes,
        BookingDate = booking.BookingDate,
        BookingTime = booking.BookingTime,
        Status = booking.Status,
        ClientName = booking.ClientName,
        ClientPhone = booking.ClientPhone,
        ClientEmail = booking.ClientEmail,
        CreatedAt = booking.CreatedAt,
        UpdatedAt = booking.UpdatedAt
    };
}
