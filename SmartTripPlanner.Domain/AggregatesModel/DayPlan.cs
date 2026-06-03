using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class DayPlan : Entity
{
    public int DayIndex { get; private set; }
    public DateOnly Date { get; private set; }
    public WeatherCondition WeatherSummary { get; private set; }
    public required BlockTimeline Morning { get; init; }
    public required BlockTimeline Afternoon { get; init; }
    public required BlockTimeline Evening { get; init; }
}
