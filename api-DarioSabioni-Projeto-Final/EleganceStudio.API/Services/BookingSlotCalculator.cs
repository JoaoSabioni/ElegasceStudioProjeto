namespace EleganceStudio.API.Services;

public static class BookingSlotCalculator
{
    public const int DefaultSlotIntervalMinutes = 30;

    public static List<TimeOnly> OccupiedSlots(
        TimeOnly start,
        int durationMinutes,
        int slotIntervalMinutes = DefaultSlotIntervalMinutes)
    {
        var slots = new List<TimeOnly>();

        if (durationMinutes <= 0 || slotIntervalMinutes <= 0)
            return slots;

        for (var minutes = 0; minutes < durationMinutes; minutes += slotIntervalMinutes)
            slots.Add(start.AddMinutes(minutes));

        return slots;
    }

    public static bool Overlaps(
        IEnumerable<TimeOnly> requestedSlots,
        TimeOnly existingStart,
        int existingDurationMinutes,
        int slotIntervalMinutes = DefaultSlotIntervalMinutes)
    {
        var existingSlots = OccupiedSlots(existingStart, existingDurationMinutes, slotIntervalMinutes);
        return requestedSlots.Any(existingSlots.Contains);
    }
}
