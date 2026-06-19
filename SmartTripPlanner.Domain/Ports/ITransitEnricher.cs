using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Enriches trip days with transit estimates between consecutive activities
/// and weather summary per day.
/// </summary>
public interface ITransitEnricher
{
    Task EnrichAsync(Trip trip, IReadOnlyDictionary<long, Place> placesById, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct);
}
