using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Pure synchronous scheduler that computes wall-clock arrival and departure times
/// for every ActivityNode in every non-empty block.
///
/// Each block starts at DayPlan.StartTime (MVP limitation — blocks are not chained).
/// Hotel transit (TransitFromHotel) and inter-activity transit advance the cursor.
/// TransitToHotel does NOT affect activity timing — it is display-only.
/// </summary>
public class TimelineScheduler : ITimelineScheduler
{
    public void Schedule(Trip trip)
    {
        foreach (var dayPlan in trip.Days)
        {
            var startMinutes = dayPlan.StartTime.Hour * 60 + dayPlan.StartTime.Minute;

            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = dayPlan.GetBlock(blockType);
                if (block.Activities.Count == 0)
                    continue;

                var currentTime = startMinutes;

                // Add hotel-to-first-activity transit at block start
                if (block.TransitFromHotel is not null)
                {
                    currentTime += block.TransitFromHotel.DurationMinutes;
                    currentTime += block.TransitFromHotel.BufferMinutes;
                }

                foreach (var activity in block.Activities)
                {
                    activity.EstimatedArrival = currentTime;
                    currentTime += activity.DurationMinutes;
                    activity.EstimatedDeparture = currentTime;

                    // Advance by inter-activity transit (excluding last activity)
                    if (activity.TransitToNext is not null)
                    {
                        currentTime += activity.TransitToNext.DurationMinutes;
                        currentTime += activity.TransitToNext.BufferMinutes;
                    }
                }

                // MVP: reset to DayPlan.StartTime for next block (no block chaining)
            }
        }
    }
}
