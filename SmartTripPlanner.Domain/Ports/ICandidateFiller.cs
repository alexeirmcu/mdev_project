using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Fills remaining block capacity with scored candidate places.
/// </summary>
public interface ICandidateFiller
{
    Task FillAsync(Trip trip, List<Place> candidatePool, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct);

    /// <summary>
    /// Fills candidates within the given scope. For CurrentBlock/CurrentDay,
    /// the caller must also pass the current day index.
    /// </summary>
    Task FillScopedAsync(Trip trip, ReplanScope scope, List<Place> candidatePool, HashSet<long> excludePlaceIds, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct);
}
