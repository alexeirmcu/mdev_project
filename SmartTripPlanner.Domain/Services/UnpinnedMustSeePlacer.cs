using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Places unpinned must-sees into days with capacity, preferring open days
/// and days with the most free slots.
/// </summary>
public class UnpinnedMustSeePlacer : IUnpinnedMustSeePlacer
{
    public bool Place(Trip trip, MustSee mustSee, Place place)
    {
        // Try each day, preferring days where the place is open
        var daysWithCapacity = trip.Days
            .Select(day => new
            {
                Day = day,
                IsOpen = ItineraryGeneratorHelpers.IsPlaceOpenOnDay(place, day.Date.DayOfWeek),
                FreeSlots = ItineraryGeneratorHelpers.GetTotalFreeSlots(day)
            })
            .OrderByDescending(d => d.IsOpen)
            .ThenByDescending(d => d.FreeSlots)
            .ToList();

        foreach (var dayInfo in daysWithCapacity)
        {
            if (dayInfo.FreeSlots <= 0)
                continue;

            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                if (!ItineraryGeneratorHelpers.CanAddActivity(dayInfo.Day, blockType, place.TypicalDurationMinutes))
                    continue;

                var activity = ItineraryGeneratorHelpers.CreateActivityNode(place, dayInfo.Day.GetBlock(blockType).Activities.Count + 1);
                dayInfo.Day.AddActivity(blockType, activity);
                return true;
            }
        }

        // Force-placement: if normal placement failed and overtime flag is on,
        // iterate days and force-place in the first block with a visit slot
        if (trip.Preferences.AllowMustSeeOvertime)
        {
            foreach (var dayInfo in daysWithCapacity)
            {
                if (dayInfo.FreeSlots <= 0)
                    continue;

                foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
                {
                    var block = dayInfo.Day.GetBlock(blockType);
                    // Overtime activities must occupy an EMPTY block exclusively.
                    if (block.Activities.Count != 0 || block.Activities.Count >= block.MaxVisits)
                        continue;

                    var activity = ItineraryGeneratorHelpers.CreateActivityNode(place, block.Activities.Count + 1);
                    dayInfo.Day.ForceAddActivity(blockType, activity);
                    return true;
                }
            }
        }

        return false;
    }
}
