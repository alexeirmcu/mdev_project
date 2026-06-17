using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class MustSee : ValueObject
{
    public long PlaceId { get; }
    public Priority Priority { get; }
    public int? PinnedDayIndex { get; }
    public BlockType? PinnedBlock { get; }

    public MustSee(long placeId, Priority priority, int? pinnedDayIndex = null, BlockType? pinnedBlock = null)
    {
        PlaceId = placeId;
        Priority = priority;
        PinnedDayIndex = pinnedDayIndex;
        PinnedBlock = pinnedBlock;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PlaceId;
        yield return Priority;
        yield return PinnedDayIndex ?? -1;
        yield return PinnedBlock ?? (BlockType)(-1);
    }
}
