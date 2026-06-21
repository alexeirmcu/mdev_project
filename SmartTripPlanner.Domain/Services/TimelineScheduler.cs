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
            var previousBlockEnd = startMinutes;

            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = dayPlan.GetBlock(blockType);
                if (block.Activities.Count == 0)
                {
                    // Empty block: still advance previousBlockEnd so non-empty blocks
                    // that chain via InterBlockTransit use the last non-empty block's end
                    continue;
                }

                int currentTime;

                // Determine block start: TransitFromHotel resets; InterBlockTransit chains; else reset
                if (block.TransitFromHotel is not null)
                {
                    currentTime = startMinutes;
                    currentTime += block.TransitFromHotel.DurationMinutes;
                    currentTime += block.TransitFromHotel.BufferMinutes;
                }
                else if (block.InterBlockTransit is not null
                         && dayPlan.GetBlock(BlockType.Morning) != block) // first block of day can't chain
                {
                    // Block arrived via inter-block transit from previous block
                    currentTime = previousBlockEnd;
                    currentTime += block.InterBlockTransit.DurationMinutes;
                    currentTime += block.InterBlockTransit.BufferMinutes;
                }
                else
                {
                    // No hotel transit and no inter-block transit → start fresh
                    currentTime = startMinutes;
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

                // Store block end time for potential chaining to next block
                previousBlockEnd = currentTime;
            }
        }
    }
}
