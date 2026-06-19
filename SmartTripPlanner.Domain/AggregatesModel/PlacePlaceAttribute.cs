namespace SmartTripPlanner.Domain.AggregatesModel;

public class PlacePlaceAttribute
{
    public long PlaceId { get; set; }
    public Place Place { get; set; } = null!;

    public long PlaceAttributeId { get; set; }
    public PlaceAttribute PlaceAttribute { get; set; } = null!;
}
