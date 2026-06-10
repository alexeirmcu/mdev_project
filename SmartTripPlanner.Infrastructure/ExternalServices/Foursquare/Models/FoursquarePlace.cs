namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

internal class FoursquarePlace
{
    public string FsqId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FoursquareGeocodes? Geocodes { get; set; }
    public FoursquareHours? Hours { get; set; }
    public List<FoursquareCategory> Categories { get; set; } = new();
}
