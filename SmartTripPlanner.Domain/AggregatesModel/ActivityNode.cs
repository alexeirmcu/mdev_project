using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class ActivityNode : Entity
{
    public int SequenceOrder { get; init; }
    public long PlaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public int EstimatedArrival { get; set; }
    public int EstimatedDeparture { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsIndoor { get; set; }
    public TransitDetails? TransitToNext { get; set; }
    public Priority Priority { get; init; } = Priority.Medium;
    public PlaceLocation Location { get; init; } = null!;
    public bool OvertimeAlert { get; private set; }

    public ActivityNode() { }

    public void MarkOvertime()
    {
        OvertimeAlert = true;
    }

    public ActivityNode(long placeId, string name, int sequenceOrder, int durationMinutes,
        bool isIndoor = false, TransitDetails? transitToNext = null, Priority priority = Priority.Medium,
        PlaceLocation? location = null)
    {
        PlaceId = placeId;
        Name = name;
        SequenceOrder = sequenceOrder;
        DurationMinutes = durationMinutes;
        IsIndoor = isIndoor;
        TransitToNext = transitToNext;
        Priority = priority;
        Location = location ?? null!;
    }

    public void SetCompleted(bool value)
    {
        IsCompleted = value;
    }
}
