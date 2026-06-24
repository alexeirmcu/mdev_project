using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Domain port for partial itinerary replanning. Supports regenerating a single day
/// in place (preserving completed activities and must-sees) and scope-driven replanning
/// from the traveler's current point forward with weather-aware swapping and pruning.
/// </summary>
public interface IItineraryReplanningEngine
{
    /// <summary>
    /// Regenerates exactly one day: preserves completed and must-see activities,
    /// clears the rest, refills via ICandidateFiller, re-enriches transit,
    /// reschedules timeline, and resets IsStale.
    /// </summary>
    Task RegenerateDayAsync(
        Trip trip,
        int dayIndex,
        IReadOnlyList<Place> candidates,
        Dictionary<DateOnly, WeatherCondition> weather,
        CancellationToken ct);

    /// <summary>
    /// Replans from the current point forward based on the ReplanContext.
    /// Handles scope resolution, weather-aware outdoor→indoor swapping,
    /// nice-to-have pruning, and stale-reset on affected days.
    /// </summary>
    Task ReplanAsync(
        Trip trip,
        ReplanContext context,
        IReadOnlyList<Place> candidates,
        Dictionary<DateOnly, WeatherCondition> weather,
        CancellationToken ct);
}

/// <summary>
/// Current replan context: the traveler's current day, block, desired scope,
/// whether the weather at their location is bad, and the current UTC time.
/// </summary>
public record ReplanContext(
    int CurrentDayIndex,
    BlockType CurrentBlock,
    ReplanScope Scope,
    bool IsBadWeather,
    DateTimeOffset CurrentDateTime);
