using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class MustSee : ValueObject
{
    public long PlaceId { get; }
    public Priority Priority { get; }
    public int? PinnedDayIndex { get; }
    public BlockType? PinnedBlock { get; }
    public bool ForceIncludeDespiteWeather { get; }

    public MustSee(long placeId, Priority priority, int? pinnedDayIndex = null, BlockType? pinnedBlock = null, bool forceIncludeDespiteWeather = false)
    {
        PlaceId = placeId;
        Priority = priority;
        PinnedDayIndex = pinnedDayIndex;
        PinnedBlock = pinnedBlock;
        ForceIncludeDespiteWeather = forceIncludeDespiteWeather;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PlaceId;
        yield return Priority;
        yield return PinnedDayIndex ?? -1;
        yield return PinnedBlock ?? (BlockType)(-1);
        yield return ForceIncludeDespiteWeather;
    }
}
