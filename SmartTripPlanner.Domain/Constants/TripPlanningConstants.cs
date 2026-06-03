namespace SmartTripPlanner.Domain.Constants;

public static class TripPlanningConstants
{
    // Block durations in minutes
    public const int MorningBlockDurationMinutes = 210;    // ~3.5h
    public const int AfternoonBlockDurationMinutes = 180;  // ~3h
    public const int EveningBlockDurationMinutes = 105;    // ~1.75h

    // Max visits per block
    public const int MaxVisitsPerMorningBlock = 3;
    public const int MaxVisitsPerAfternoonBlock = 3;
    public const int MaxVisitsPerEveningBlock = 2;

    // Buffers in minutes
    public const int DefaultTransitBufferMinutes = 10;
    public const int DefaultActivityBufferMinutes = 15;

    // Default start hour (minutes from midnight): 09:00 = 540
    public const int DefaultStartHourMinutes = 540;
}
