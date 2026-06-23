using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather;

internal interface IWeatherApiClient
{
    Task<IReadOnlyList<OpenMeteoDailyForecast>> GetForecastAsync(
        double latitude,
        double longitude,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
