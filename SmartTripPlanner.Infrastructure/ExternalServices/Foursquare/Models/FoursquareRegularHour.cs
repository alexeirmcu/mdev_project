namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

public class FoursquareRegularHour
{
    public int Day { get; set; }
    public string Open { get; set; } = string.Empty;
    public string Close { get; set; } = string.Empty;
}
