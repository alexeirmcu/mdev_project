using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Shared static helper methods used by the itinerary generator and its collaborators.
/// </summary>
public static class ItineraryGeneratorHelpers
{
    public static ActivityNode CreateActivityNode(Place place, int sequenceOrder)
    {
        return new ActivityNode(
            place.Id,
            place.Name,
            sequenceOrder,
            place.TypicalDurationMinutes,
            place.IsIndoor,
            transitToNext: null,
            priority: Priority.Medium,
            location: place.Location);
    }

    public static bool IsPlaceOpenOnDay(Place place, DayOfWeek dayOfWeek)
    {
        if (place.OpeningHours.Count == 0)
            return true; // No hours data — assume always open

        return place.OpeningHours.Any(oh => oh.IsOpenOn(dayOfWeek));
    }

    public static bool CanAddActivity(DayPlan dayPlan, BlockType blockType, int durationMinutes)
    {
        var block = dayPlan.GetBlock(blockType);
        return block.CanFitActivity(durationMinutes);
    }

    public static int GetTotalFreeSlots(DayPlan dayPlan)
    {
        return (TripPlanningConstants.MaxVisitsPerMorningBlock - dayPlan.Morning.Activities.Count)
             + (TripPlanningConstants.MaxVisitsPerAfternoonBlock - dayPlan.Afternoon.Activities.Count)
             + (TripPlanningConstants.MaxVisitsPerEveningBlock - dayPlan.Evening.Activities.Count);
    }

    public static int GetBlockMaxVisits(BlockType blockType) => blockType switch
    {
        BlockType.Morning => TripPlanningConstants.MaxVisitsPerMorningBlock,
        BlockType.Afternoon => TripPlanningConstants.MaxVisitsPerAfternoonBlock,
        BlockType.Evening => TripPlanningConstants.MaxVisitsPerEveningBlock,
        _ => 0
    };

    public static BlockType[] GetAdjacentBlocks(BlockType blockType) => blockType switch
    {
        BlockType.Morning => new[] { BlockType.Afternoon },
        BlockType.Afternoon => new[] { BlockType.Morning, BlockType.Evening },
        BlockType.Evening => new[] { BlockType.Afternoon },
        _ => Array.Empty<BlockType>()
    };
}
