using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class BlockTimeline : Entity
{
    public BlockType BlockType { get; init; }
    public TransitDetails? TransitFromHotel { get; set; }
    public TransitDetails? TransitToHotel { get; set; }
    public TransitDetails? InterBlockTransit { get; set; }
    public int BlockTotalDurationMinutes => Activities.Sum(a => a.DurationMinutes + (a.TransitToNext?.DurationMinutes ?? 0));
    public int BlockWallClockDurationMinutes =>
        (TransitFromHotel?.DurationMinutes ?? 0) +
        BlockTotalDurationMinutes +
        (TransitToHotel?.DurationMinutes ?? 0);
    public List<ActivityNode> Activities { get; private set; } = new();

    public int MaxDurationMinutes => GetBlockConstraints().maxDuration;
    public int MaxVisits => GetBlockConstraints().maxVisits;

    public void AddActivity(ActivityNode activity)
    {
        var (maxVisits, maxDuration) = GetBlockConstraints();

        if (Activities.Count >= maxVisits)
            throw new InvalidOperationException($"Block {BlockType} already has maximum visits ({maxVisits}).");

        var newTotal = BlockTotalDurationMinutes + activity.DurationMinutes + (activity.TransitToNext?.DurationMinutes ?? 0);
        if (newTotal > maxDuration)
            throw new InvalidOperationException($"Adding activity exceeds maximum duration ({maxDuration} min) for {BlockType} block.");

        Activities.Add(activity);
    }

    public void ForceAddActivity(ActivityNode activity)
    {
        var (maxVisits, _) = GetBlockConstraints();

        if (Activities.Count >= maxVisits)
            throw new InvalidOperationException($"Block {BlockType} already has maximum visits ({maxVisits}).");

        activity.MarkOvertime();
        Activities.Add(activity);
    }

    public void RemoveActivity(ActivityNode activity)
    {
        Activities.Remove(activity);
    }

    public bool CanFitActivity(int durationMinutes)
    {
        var (maxVisits, maxDuration) = GetBlockConstraints();

        if (Activities.Count >= maxVisits)
            return false;

        return BlockTotalDurationMinutes + durationMinutes <= maxDuration;
    }

    private (int maxVisits, int maxDuration) GetBlockConstraints()
    {
        return BlockType switch
        {
            BlockType.Morning => (TripPlanningConstants.MaxVisitsPerMorningBlock, TripPlanningConstants.MorningBlockDurationMinutes),
            BlockType.Afternoon => (TripPlanningConstants.MaxVisitsPerAfternoonBlock, TripPlanningConstants.AfternoonBlockDurationMinutes),
            BlockType.Evening => (TripPlanningConstants.MaxVisitsPerEveningBlock, TripPlanningConstants.EveningBlockDurationMinutes),
            _ => throw new InvalidOperationException($"Unknown block type: {BlockType}")
        };
    }
}
