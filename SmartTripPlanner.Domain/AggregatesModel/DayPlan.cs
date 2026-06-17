using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class DayPlan : Entity
{
    public int DayIndex { get; init; }
    public DateOnly Date { get; init; }
    public WeatherCondition WeatherSummary { get; private set; }
    public required BlockTimeline Morning { get; init; }
    public required BlockTimeline Afternoon { get; init; }
    public required BlockTimeline Evening { get; init; }
    public TimeOnly StartTime { get; private set; } = new TimeOnly(9, 0);

    public void UpdateStartTime(TimeOnly newStart)
    {
        StartTime = newStart;
    }

    public void SetWeather(WeatherCondition weather)
    {
        WeatherSummary = weather;
    }

    internal BlockTimeline GetBlock(BlockType blockType) => blockType switch
    {
        BlockType.Morning => Morning,
        BlockType.Afternoon => Afternoon,
        BlockType.Evening => Evening,
        _ => throw new ArgumentOutOfRangeException(nameof(blockType), blockType, null)
    };

    public void AddActivity(BlockType blockType, ActivityNode activity)
    {
        GetBlock(blockType).AddActivity(activity);
    }

    public void RemoveActivity(BlockType blockType, ActivityNode activity)
    {
        GetBlock(blockType).RemoveActivity(activity);
    }
}
