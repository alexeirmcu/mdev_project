using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class DayPlan : Entity
{
    private readonly List<BlockTimeline> _blocks = new();

    public int DayIndex { get; init; }
    public DateOnly Date { get; init; }
    public WeatherCondition WeatherSummary { get; private set; }
    public IReadOnlyList<BlockTimeline> Blocks => _blocks.AsReadOnly();
    public TimeOnly StartTime { get; private set; } = new TimeOnly(9, 0);
    public bool IsStale { get; private set; }
    public DateTimeOffset? WeatherLastUpdatedAt { get; private set; }

    internal DayPlan() { } // EF Core

    public DayPlan(int dayIndex, DateOnly date,
        BlockTimeline morning, BlockTimeline afternoon, BlockTimeline evening)
    {
        DayIndex = dayIndex;
        Date = date;

        if (morning.BlockType != BlockType.Morning)
            throw new ArgumentException($"Expected BlockType.Morning but got {morning.BlockType}", nameof(morning));
        if (afternoon.BlockType != BlockType.Afternoon)
            throw new ArgumentException($"Expected BlockType.Afternoon but got {afternoon.BlockType}", nameof(afternoon));
        if (evening.BlockType != BlockType.Evening)
            throw new ArgumentException($"Expected BlockType.Evening but got {evening.BlockType}", nameof(evening));

        _blocks = new List<BlockTimeline> { morning, afternoon, evening };
    }

    public void UpdateStartTime(TimeOnly newStart)
    {
        StartTime = newStart;
    }

    public void SetWeather(WeatherCondition weather)
    {
        WeatherSummary = weather;
    }

    public void MarkStale()
    {
        IsStale = true;
    }

    public void ClearStale()
    {
        IsStale = false;
    }

    public void UpdateWeatherTimestamp()
    {
        WeatherLastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public BlockTimeline GetBlock(BlockType blockType) => Blocks[(int)blockType];

    public void AddActivity(BlockType blockType, ActivityNode activity)
    {
        GetBlock(blockType).AddActivity(activity);
    }

    public void ForceAddActivity(BlockType blockType, ActivityNode activity)
    {
        GetBlock(blockType).ForceAddActivity(activity);
    }

    public void RemoveActivity(BlockType blockType, ActivityNode activity)
    {
        GetBlock(blockType).RemoveActivity(activity);
    }
}
