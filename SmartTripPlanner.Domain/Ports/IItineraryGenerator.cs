using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Domain port for itinerary generation. Implementations produce
/// a sequence of DayPlan activities from trip must-sees and candidate places.
/// </summary>
public interface IItineraryGenerator
{
    /// <summary>
    /// Generates a complete itinerary for the trip, populating Trip.Days
    /// with activities and transit. Throws OverConstrainedRouteException
    /// if High-priority must-sees cannot fit after all fallback attempts.
    /// </summary>
    Task GenerateAsync(
        Trip trip,
        IReadOnlyList<Place> candidatePlaces,
        Dictionary<DateOnly, WeatherCondition> weatherData,
        CancellationToken ct = default);
}
