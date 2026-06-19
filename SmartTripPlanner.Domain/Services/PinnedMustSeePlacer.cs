using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Places pinned must-sees at their exact day/block, with overflow to adjacent blocks.
/// </summary>
public class PinnedMustSeePlacer : IPinnedMustSeePlacer
{
    public bool Place(Trip trip, MustSee mustSee, Place place)
    {
        var dayIndex = mustSee.PinnedDayIndex!.Value;
        if (dayIndex < 0 || dayIndex >= trip.Days.Count)
            return false;

        var dayPlan = trip.Days[dayIndex];

        var targetBlocks = mustSee.PinnedBlock.HasValue
            ? new[] { mustSee.PinnedBlock.Value }
            : new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening };

        foreach (var blockType in targetBlocks)
        {
            if (!ItineraryGeneratorHelpers.IsPlaceOpenOnDay(place, dayPlan.Date.DayOfWeek))
                continue;

            if (!ItineraryGeneratorHelpers.CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                continue;

            var activity = ItineraryGeneratorHelpers.CreateActivityNode(place, dayPlan.GetBlock(blockType).Activities.Count + 1);
            dayPlan.AddActivity(blockType, activity);
            return true;
        }

        // Try overflow to adjacent blocks of the same day
        foreach (var blockType in ItineraryGeneratorHelpers.GetAdjacentBlocks(targetBlocks.First()))
        {
            if (!ItineraryGeneratorHelpers.CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                continue;

            var activity = ItineraryGeneratorHelpers.CreateActivityNode(place, dayPlan.GetBlock(blockType).Activities.Count + 1);
            dayPlan.AddActivity(blockType, activity);
            return true;
        }

        return false;
    }
}
