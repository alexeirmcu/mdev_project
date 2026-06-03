using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class BlockTimeline : Entity
{
    public int BlockTotalDurationMinutes { get; private set; }
    public List<ActivityNode> Activities { get; private set; } = new();
}
