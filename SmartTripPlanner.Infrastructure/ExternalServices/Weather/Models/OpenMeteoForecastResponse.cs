using System.Text.Json.Serialization;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather.Models;

internal sealed class OpenMeteoForecastResponse
{
    [JsonPropertyName("daily")]
    public OpenMeteoDailyData Daily { get; set; } = new();
}

internal sealed class OpenMeteoDailyData
{
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = new();

    [JsonPropertyName("weather_code")]
    public List<int> WeatherCode { get; set; } = new();

    [JsonPropertyName("temperature_2m_max")]
    public List<double> TemperatureMax { get; set; } = new();

    [JsonPropertyName("temperature_2m_min")]
    public List<double> TemperatureMin { get; set; } = new();
}
