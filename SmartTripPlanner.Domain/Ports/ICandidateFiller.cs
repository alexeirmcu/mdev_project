using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Fills remaining block capacity with scored candidate places.
/// </summary>
public interface ICandidateFiller
{
    Task FillAsync(Trip trip, List<Place> candidatePool, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct);
}
