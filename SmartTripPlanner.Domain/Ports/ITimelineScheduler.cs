using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Pure synchronous scheduler that computes EstimatedArrival and EstimatedDeparture
/// for every ActivityNode in every non-empty block, based on DayPlan.StartTime,
/// hotel transit, inter-activity transit, and buffers.
/// </summary>
public interface ITimelineScheduler
{
    void Schedule(Trip trip);
}
