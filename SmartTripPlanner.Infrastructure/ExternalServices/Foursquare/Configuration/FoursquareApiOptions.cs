namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Configuration;

public class FoursquareApiOptions
{
    public const string SectionName = "FoursquareApi";
    public string BaseUrl { get; set; } = "https://places-api.foursquare.com/";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2025-06-17";
}
