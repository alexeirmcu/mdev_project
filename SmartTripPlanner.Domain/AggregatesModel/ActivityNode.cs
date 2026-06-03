using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class ActivityNode : Entity
{
    public int SequenceOrder { get; private set; }
    public required string PlaceId { get; init; }
    public required string Name { get; init; }
    public bool IsCompleted { get; private set; }
    public int EstimatedArrival { get; private set; }
    public int EstimatedDeparture { get; private set; }
    public int DurationMinutes { get; private set; }
    public bool IsIndoor { get; private set; }
    public TransitDetails? TransitToNext { get; private set; }
}
