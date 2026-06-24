using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Domain service for partial itinerary replanning. Preserves completed activities
/// and must-sees, delegates candidate filling, transit enrichment, and timeline
/// scheduling to the existing domain collaborators.
/// </summary>
public class ItineraryReplanningEngine : IItineraryReplanningEngine
{
    private readonly ICandidateFiller _filler;
    private readonly ITransitEnricher _enricher;
    private readonly ITimelineScheduler _scheduler;

    public ItineraryReplanningEngine(
        ICandidateFiller filler,
        ITransitEnricher enricher,
        ITimelineScheduler scheduler)
    {
        _filler = filler ?? throw new ArgumentNullException(nameof(filler));
        _enricher = enricher ?? throw new ArgumentNullException(nameof(enricher));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    /// <inheritdoc />
    public async Task RegenerateDayAsync(
        Trip trip,
        int dayIndex,
        IReadOnlyList<Place> candidates,
        Dictionary<DateOnly, WeatherCondition> weather,
        CancellationToken ct)
    {
        if (dayIndex < 0 || dayIndex >= trip.Days.Count)
            throw new ArgumentOutOfRangeException(nameof(dayIndex),
                $"DayIndex {dayIndex} is out of range. Trip has {trip.Days.Count} days.");

        var targetDay = trip.Days[dayIndex];

        // 1. LOCK: preserve completed activities (keep in place)
        // 2. PRESERVE: must-sees assigned to this day (by PlaceId matching OriginalMustSees)
        var mustSeePlaceIds = new HashSet<long>(
            trip.OriginalMustSees.Select(m => m.PlaceId));

        // 3. CLEAR: remove non-completed, non-must-see activities from the target day's blocks
        foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
        {
            var block = targetDay.GetBlock(blockType);
            var toRemove = block.Activities
                .Where(a => !a.IsCompleted && !mustSeePlaceIds.Contains(a.PlaceId))
                .ToList();

            foreach (var activity in toRemove)
            {
                block.RemoveActivity(activity);
            }
        }

        // 4. Collect excludePlaceIds: all PlaceIds from remaining activities across ALL days
        var excludePlaceIds = new HashSet<long>();
        foreach (var day in trip.Days)
        {
            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = day.GetBlock(blockType);
                foreach (var activity in block.Activities)
                {
                    excludePlaceIds.Add(activity.PlaceId);
                }
            }
        }

        // 5. Build placesById dictionary for enricher
        var placesById = candidates.ToDictionary(p => p.Id);

        // 6. REFILL via FillScopedAsync
        var candidateList = candidates.ToList();
        await _filler.FillScopedAsync(trip, ReplanScope.CurrentDay, candidateList,
            excludePlaceIds, weather, ct);

        // 7. ENRICH via EnrichScopedAsync
        await _enricher.EnrichScopedAsync(trip, ReplanScope.CurrentDay,
            placesById, weather, ct);

        // 8. SCHEDULE via ScheduleScoped for just the target day
        _scheduler.ScheduleScoped(trip, new List<int> { dayIndex }, 0);

        // 9. Clear stale on the target day
        targetDay.ClearStale();
    }

    /// <inheritdoc />
    public async Task ReplanAsync(
        Trip trip,
        ReplanContext context,
        IReadOnlyList<Place> candidates,
        Dictionary<DateOnly, WeatherCondition> weather,
        CancellationToken ct)
    {
        var (currentDayIndex, currentBlock, scope, isBadWeather, currentDateTime) = context;

        // Determine which (dayIndex, blockType) pairs are in scope
        var inScope = GetScopeBlocks(trip, currentDayIndex, currentBlock, scope);

        // 1. LOCK: preserve completed activities across ALL days (keep in place)
        // 2. LOCK past: days before CurrentDayIndex are completely locked (skip in iteration)
        // 3. LOCK pre-current blocks: blocks before CurrentBlock are locked
        // 4. PRESERVE must-sees in scope (by PlaceId)
        var mustSeePlaceIds = new HashSet<long>(
            trip.OriginalMustSees.Select(m => m.PlaceId));

        var forcedMustSeePlaceIds = new HashSet<long>(
            trip.OriginalMustSees
                .Where(m => m.ForceIncludeDespiteWeather)
                .Select(m => m.PlaceId));

        // 5. Weather-aware swap: outdoor → indoor on Bad weather (for scope blocks).
        //    Replaces outdoor non-completed, non-must-see activities with indoor alternatives.
        //    Forced outdoor must-sees (ForceIncludeDespiteWeather) are retained.
        if (isBadWeather)
        {
            foreach (var (dayIdx, blockType) in inScope)
            {
                var day = trip.Days[dayIdx];
                var block = day.GetBlock(blockType);

                // Identify outdoor non-completed activities that are NOT forced must-sees
                var toSwap = block.Activities
                    .Where(a => !a.IsCompleted
                                && !a.IsIndoor
                                && !mustSeePlaceIds.Contains(a.PlaceId))
                    .ToList();

                foreach (var activity in toSwap)
                {
                    block.RemoveActivity(activity);
                }
            }
        }

        // 6. Nice-to-have pruning: if behind schedule, remove Priority.Low
        //    non-must-see from the current block to recover time.
        if (IsBehindSchedule(trip.Days[currentDayIndex], currentBlock, currentDateTime))
        {
            var currentDayBlock = trip.Days[currentDayIndex].GetBlock(currentBlock);
            var toPrune = currentDayBlock.Activities
                .Where(a => !a.IsCompleted
                            && !mustSeePlaceIds.Contains(a.PlaceId)
                            && a.Priority == Priority.Low)
                .ToList();

            foreach (var activity in toPrune)
            {
                currentDayBlock.RemoveActivity(activity);
            }
        }

        // 8. Collect excludePlaceIds from remaining activities across all scope days
        var excludePlaceIds = new HashSet<long>();
        foreach (var (dayIdx, blockType) in inScope)
        {
            var day = trip.Days[dayIdx];
            var block = day.GetBlock(blockType);
            foreach (var activity in block.Activities)
            {
                excludePlaceIds.Add(activity.PlaceId);
            }
        }

        // Also include activities from locked days (to prevent duplicates)
        foreach (var day in trip.Days.Where(d => d.DayIndex < currentDayIndex))
        {
            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = day.GetBlock(blockType);
                foreach (var activity in block.Activities)
                {
                    excludePlaceIds.Add(activity.PlaceId);
                }
            }
        }

        // 9. Build placesById for enricher
        var placesById = candidates.ToDictionary(p => p.Id);

        // 10. REFILL via FillScopedAsync
        var candidateList = candidates.ToList();
        await _filler.FillScopedAsync(trip, scope, candidateList,
            excludePlaceIds, weather, ct);

        // 11. ENRICH via EnrichScopedAsync
        await _enricher.EnrichScopedAsync(trip, scope,
            placesById, weather, ct);

        // 12. SCHEDULE for affected days
        var affectedDayIndices = inScope
            .Select(t => t.dayIndex)
            .Distinct()
            .OrderBy(i => i)
            .ToList();
        _scheduler.ScheduleScoped(trip, affectedDayIndices, 0);

        // 13. Clear stale on all affected days
        foreach (var dayIdx in affectedDayIndices)
        {
            trip.Days[dayIdx].ClearStale();
        }
    }

    /// <summary>
    /// Returns the set of (dayIndex, blockType) pairs that are in scope
    /// for the replan operation.
    /// </summary>
    private static List<(int dayIndex, BlockType blockType)> GetScopeBlocks(
        Trip trip, int currentDayIndex, BlockType currentBlock, ReplanScope scope)
    {
        var result = new List<(int dayIndex, BlockType blockType)>();
        var allBlocks = new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening };

        foreach (var day in trip.Days)
        {
            if (day.DayIndex < currentDayIndex)
                continue; // Fully locked

            if (day.DayIndex == currentDayIndex)
            {
                bool inScopeForDay = scope != ReplanScope.CurrentBlock;

                foreach (var blockType in allBlocks)
                {
                    if (blockType < currentBlock)
                        continue; // Pre-current block locked

                    if (scope == ReplanScope.CurrentBlock && blockType != currentBlock)
                        continue; // Only current block

                    result.Add((day.DayIndex, blockType));
                }

                if (scope == ReplanScope.CurrentBlock)
                    break; // Only one block
            }
            else if (day.DayIndex > currentDayIndex)
            {
                if (scope == ReplanScope.RemainingTrip)
                {
                    foreach (var blockType in allBlocks)
                    {
                        result.Add((day.DayIndex, blockType));
                    }
                }
                // else: not in scope (CurrentBlock or CurrentDay only)
            }
        }

        return result;
    }

    /// <summary>
    /// Determines if the traveler is behind schedule for the current block.
    /// Returns true when CurrentDateTime's time-of-day is past the day's start time
    /// plus an estimated travel buffer (~60 minutes for the first block setup).
    /// </summary>
    private static bool IsBehindSchedule(
        DayPlan dayPlan, BlockType currentBlock, DateTimeOffset currentDateTime)
    {
        var dayStartMinutes = dayPlan.StartTime.Hour * 60 + dayPlan.StartTime.Minute;
        var currentMinutes = currentDateTime.Hour * 60 + currentDateTime.Minute;

        // Behind schedule if current time is past the block's planned start
        // (day start + estimated setup/hotel transit buffer)
        const int estimatedSetupBufferMinutes = 30;
        return currentMinutes > dayStartMinutes + estimatedSetupBufferMinutes;
    }
}
