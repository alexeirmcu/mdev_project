namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather.Configuration;

public class OpenMeteoApiOptions
{
    public const string SectionName = "OpenMeteoApi";
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";
    public int TimeoutSeconds { get; set; } = 5;
}
