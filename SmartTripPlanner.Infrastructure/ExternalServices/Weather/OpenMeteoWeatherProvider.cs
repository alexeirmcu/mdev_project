using Microsoft.Extensions.Logging;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Mapping;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather;

internal sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly ICityRepository _cityRepository;
    private readonly IWeatherApiClient _apiClient;
    private readonly ILogger<OpenMeteoWeatherProvider> _logger;

    public OpenMeteoWeatherProvider(
        ICityRepository cityRepository,
        IWeatherApiClient apiClient,
        ILogger<OpenMeteoWeatherProvider> logger)
    {
        _cityRepository = cityRepository;
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<Dictionary<DateOnly, WeatherCondition>> GetWeatherAsync(
        long cityId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        // Pre-fill all dates with Clear
        var result = new Dictionary<DateOnly, WeatherCondition>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
            result[date] = WeatherCondition.Clear;

        // Resolve city coordinates
        var city = await _cityRepository.GetByIdAsync(cityId, ct);
        if (city is null || city.Latitude is null || city.Longitude is null)
        {
            _logger.LogWarning(
                "Weather cannot be fetched for city {CityId} because coordinates are missing",
                cityId);
            return result;
        }

        // Fetch forecast from API
        var forecasts = await _apiClient.GetForecastAsync(
            city.Latitude.Value, city.Longitude.Value, startDate, endDate, ct);

        if (forecasts.Count == 0)
        {
            _logger.LogWarning(
                "Weather API returned no data for city {CityId} ({Latitude}, {Longitude}) from {StartDate} to {EndDate}",
                cityId, city.Latitude, city.Longitude, startDate, endDate);
            return result;
        }

        // Map each forecast day
        foreach (var forecast in forecasts)
        {
            if (result.ContainsKey(forecast.Date))
            {
                result[forecast.Date] = WeatherCodeMapper.Map(
                    forecast.WeatherCode, forecast.TempMax, forecast.TempMin);
            }
        }

        return result;
    }
}
