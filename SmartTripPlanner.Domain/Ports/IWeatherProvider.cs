using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Domain port for fetching weather forecasts per date range.
/// MVP uses a stubbed implementation returning Clear for all dates.
/// </summary>
public interface IWeatherProvider
{
    Task<Dictionary<DateOnly, WeatherCondition>> GetWeatherAsync(
        long cityId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
