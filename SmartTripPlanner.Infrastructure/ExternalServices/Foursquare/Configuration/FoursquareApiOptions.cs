namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Configuration;

public class FoursquareApiOptions
{
    public const string SectionName = "FoursquareApi";
    public string BaseUrl { get; set; } = "https://api.foursquare.com/v3/";
    public string ApiKey { get; set; } = string.Empty;
}
