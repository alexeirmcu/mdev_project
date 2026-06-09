using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class DayPlan : Entity
{
    public int DayIndex { get; init; }
    public DateOnly Date { get; init; }
    public WeatherCondition WeatherSummary { get; init; }
    public required BlockTimeline Morning { get; init; }
    public required BlockTimeline Afternoon { get; init; }
    public required BlockTimeline Evening { get; init; }
    public TimeOnly StartTime { get; private set; } = new TimeOnly(9, 0);

    public void UpdateStartTime(TimeOnly newStart)
    {
        StartTime = newStart;
    }
}
