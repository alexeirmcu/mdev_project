namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

internal class FoursquareChain
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

internal class FoursquarePlace
{
    public string FsqPlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public FoursquareHours? Hours { get; set; }
    public List<FoursquareCategory> Categories { get; set; } = new();
    public List<FoursquareChain> Chains { get; set; } = new();
    public List<FoursquareTip> Tips { get; set; } = new();
}
