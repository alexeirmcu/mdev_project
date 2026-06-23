using System.Net.Http.Json;
using System.Text.Json;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather;

internal class OpenMeteoApiClient : IWeatherApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public OpenMeteoApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<OpenMeteoDailyForecast>> GetForecastAsync(
        double latitude,
        double longitude,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/v1/forecast?latitude={latitude}&longitude={longitude}" +
                      $"&daily=weather_code,temperature_2m_max,temperature_2m_min" +
                      $"&timezone=auto&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}";

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return Array.Empty<OpenMeteoDailyForecast>();

            var json = await response.Content.ReadAsStringAsync(ct);
            var wrapper = JsonSerializer.Deserialize<OpenMeteoForecastResponse>(json, JsonOptions);

            if (wrapper?.Daily is null ||
                wrapper.Daily.Time.Count == 0)
                return Array.Empty<OpenMeteoDailyForecast>();

            var results = new List<OpenMeteoDailyForecast>(wrapper.Daily.Time.Count);
            for (int i = 0; i < wrapper.Daily.Time.Count; i++)
            {
                if (!DateOnly.TryParse(wrapper.Daily.Time[i], out var date))
                    continue;

                var weatherCode = i < wrapper.Daily.WeatherCode.Count ? wrapper.Daily.WeatherCode[i] : 0;
                var tempMax = i < wrapper.Daily.TemperatureMax.Count ? wrapper.Daily.TemperatureMax[i] : 0.0;
                var tempMin = i < wrapper.Daily.TemperatureMin.Count ? wrapper.Daily.TemperatureMin[i] : 0.0;

                results.Add(new OpenMeteoDailyForecast(date, weatherCode, tempMax, tempMin));
            }

            return results;
        }
        catch (HttpRequestException)
        {
            return Array.Empty<OpenMeteoDailyForecast>();
        }
        catch (TaskCanceledException)
        {
            return Array.Empty<OpenMeteoDailyForecast>();
        }
    }
}
