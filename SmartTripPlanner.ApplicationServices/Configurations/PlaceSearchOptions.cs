namespace SmartTripPlanner.ApplicationServices.Configurations;

public class PlaceSearchOptions
{
    public const string SectionName = "PlaceSearch";

    public int MaxResults { get; set; } = 10;
}
