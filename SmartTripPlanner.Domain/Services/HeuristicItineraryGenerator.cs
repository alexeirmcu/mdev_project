using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Core heuristic itinerary generator implementing a 5-phase algorithm:
/// 1. Generate empty DayPlans
/// 2. Place pinned must-sees (exact day/block)
/// 3. Place unpinned must-sees (zone-clustered)
/// 4. Fill remaining capacity with scored candidates
/// 5. Enrich with transit estimates and weather
/// </summary>
public class HeuristicItineraryGenerator : IItineraryGenerator
{
    private readonly ICandidateScorer _scorer;
    private readonly ITransitCalculator _transitCalculator;
    private Dictionary<long, Place> _placesById = null!;

    public HeuristicItineraryGenerator(
        ICandidateScorer scorer,
        ITransitCalculator transitCalculator)
    {
        _scorer = scorer;
        _transitCalculator = transitCalculator;
    }

    public async Task GenerateAsync(
        Trip trip,
        IReadOnlyList<Place> allPlaces,
        Dictionary<DateOnly, WeatherCondition> weatherData,
        CancellationToken ct)
    {
        // Phase 1: Create empty DayPlans for each trip day
        trip.GenerateDays();

        // Build lookup structures
        _placesById = allPlaces.ToDictionary(p => p.Id);
        var mustSeeIds = new HashSet<long>(trip.OriginalMustSees.Select(m => m.PlaceId));

        // Separate must-see places from pure candidate places
        var mustSeeEntries = new List<(MustSee mustSee, Place place)>();
        foreach (var mustSee in trip.OriginalMustSees)
        {
            if (_placesById.TryGetValue(mustSee.PlaceId, out var place))
                mustSeeEntries.Add((mustSee, place));
        }

        var candidatePool = allPlaces
            .Where(p => !mustSeeIds.Contains(p.Id))
            .ToList();

        // Track unplaced must-sees for fallback
        var unplacedHigh = new List<long>();

        // Phase 2: Place pinned must-sees (exact day/block)
        var pinnedMustSees = mustSeeEntries.Where(e => e.mustSee.PinnedDayIndex.HasValue).ToList();
        var unpinnedMustSees = mustSeeEntries.Where(e => !e.mustSee.PinnedDayIndex.HasValue).ToList();

        foreach (var (mustSee, place) in pinnedMustSees)
        {
            if (!TryPlacePinnedMustSee(trip, mustSee, place))
            {
                if (mustSee.Priority == Priority.High)
                    unplacedHigh.Add(mustSee.PlaceId);
            }
        }

        // Phase 3: Place unpinned must-sees using zone clustering
        var unclusteredPlaces = unpinnedMustSees
            .Where(e => !unplacedHigh.Contains(e.mustSee.PlaceId))
            .Select(e => e.place)
            .ToList();

        var clusters = ZoneClusteringHelper.Cluster(unclusteredPlaces);

        foreach (var cluster in clusters)
        {
            var clusterEntries = unpinnedMustSees
                .Where(e => cluster.Any(p => p.Id == e.place.Id))
                .OrderByDescending(e => e.mustSee.Priority)
                .ToList();

            foreach (var (mustSee, place) in clusterEntries)
            {
                if (unplacedHigh.Contains(mustSee.PlaceId))
                    continue;

                if (!TryPlaceUnpinnedMustSee(trip, mustSee, place))
                {
                    if (mustSee.Priority == Priority.High)
                        unplacedHigh.Add(mustSee.PlaceId);
                }
            }
        }

        // Phase 4: Fill remaining block capacity with scored candidates
        await FillCandidatesAsync(trip, candidatePool, weatherData, ct);

        // Phase 5: Enrich with transit estimates and weather
        await EnrichTransitAndWeatherAsync(trip, weatherData, ct);

        // Fallback chain: if High must-sees remain unplaced, throw
        if (unplacedHigh.Count > 0)
        {
            throw new OverConstrainedRouteException(unplacedHigh.AsReadOnly());
        }
    }

    private bool TryPlacePinnedMustSee(Trip trip, MustSee mustSee, Place place)
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
            if (!IsPlaceOpenOnDay(place, dayPlan.Date.DayOfWeek))
                continue;

            if (!CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                continue;

            var activity = CreateActivityNode(place, dayPlan.GetBlock(blockType).Activities.Count + 1);
            dayPlan.AddActivity(blockType, activity);
            return true;
        }

        // Try overflow to adjacent blocks of the same day
        foreach (var blockType in GetAdjacentBlocks(targetBlocks.First()))
        {
            if (!CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                continue;

            var activity = CreateActivityNode(place, dayPlan.GetBlock(blockType).Activities.Count + 1);
            dayPlan.AddActivity(blockType, activity);
            return true;
        }

        return false;
    }

    private static bool TryPlaceUnpinnedMustSee(Trip trip, MustSee mustSee, Place place)
    {
        // Try each day, preferring days where the place is open
        var daysWithCapacity = trip.Days
            .Select(day => new
            {
                Day = day,
                IsOpen = IsPlaceOpenOnDay(place, day.Date.DayOfWeek),
                FreeSlots = GetTotalFreeSlots(day)
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
                if (!CanAddActivity(dayInfo.Day, blockType, place.TypicalDurationMinutes))
                    continue;

                var activity = CreateActivityNode(place, dayInfo.Day.GetBlock(blockType).Activities.Count + 1);
                dayInfo.Day.AddActivity(blockType, activity);
                return true;
            }
        }

        return false;
    }

    private async Task FillCandidatesAsync(
        Trip trip,
        List<Place> candidatePool,
        Dictionary<DateOnly, WeatherCondition> weatherData,
        CancellationToken ct)
    {
        if (candidatePool.Count == 0)
            return;

        var usedPlaceIds = new HashSet<long>(trip.OriginalMustSees.Select(m => m.PlaceId));

        foreach (var dayPlan in trip.Days)
        {
            var isBadWeather = weatherData.TryGetValue(dayPlan.Date, out var weather)
                               && weather == WeatherCondition.Bad;

            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = dayPlan.GetBlock(blockType);
                var remainingCapacity = GetBlockMaxVisits(blockType) - block.Activities.Count;

                if (remainingCapacity <= 0)
                    continue;

                var availableCandidates = candidatePool
                    .Where(p => !usedPlaceIds.Contains(p.Id)
                                && IsPlaceOpenOnDay(p, dayPlan.Date.DayOfWeek))
                    .ToList();

                if (availableCandidates.Count == 0)
                    continue;

                // Score and rank candidates
                var scored = availableCandidates
                    .Select(p =>
                    {
                        var distance = EstimateDistanceFromNearestActivity(p, block.Activities, dayPlan);
                        var context = new ScoringContext(
                            IsFamilyTrip: trip.Travelers.Children > 0,
                            IsBadWeather: isBadWeather && trip.Preferences.WeatherAwareEnabled,
                            DistanceFromBlockCenterKm: distance,
                            PopularityRaw: 0.5);
                        var score = _scorer.Score(p, context);
                        return (Place: p, Score: score);
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                foreach (var (place, _) in scored)
                {
                    if (remainingCapacity <= 0)
                        break;

                    if (!CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                        continue;

                    var activity = CreateActivityNode(place, block.Activities.Count + 1);
                    dayPlan.AddActivity(blockType, activity);
                    usedPlaceIds.Add(place.Id);
                    remainingCapacity--;
                }
            }
        }
    }

    private async Task EnrichTransitAndWeatherAsync(
        Trip trip,
        Dictionary<DateOnly, WeatherCondition> weatherData,
        CancellationToken ct)
    {
        foreach (var dayPlan in trip.Days)
        {
            // Set weather summary
            if (weatherData.TryGetValue(dayPlan.Date, out var weather))
                dayPlan.SetWeather(weather);

            // Calculate transit between consecutive activities in each block
            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var activities = dayPlan.GetBlock(blockType).Activities;
                for (int i = 0; i < activities.Count; i++)
                {
                    if (i < activities.Count - 1)
                    {
                        var fromActivity = activities[i];
                        var toActivity = activities[i + 1];

                        if (_placesById.TryGetValue(fromActivity.PlaceId, out var fromPlace)
                            && _placesById.TryGetValue(toActivity.PlaceId, out var toPlace)
                            && fromPlace.Location is not null
                            && toPlace.Location is not null)
                        {
                            var transit = await AssignTransitAsync(
                                fromPlace.Location,
                                toPlace.Location,
                                trip.Preferences,
                                ct);

                            fromActivity.TransitToNext = transit;
                        }
                    }
                }
            }
        }
    }

    private async Task<TransitDetails> AssignTransitAsync(
        PlaceLocation from,
        PlaceLocation to,
        TripPreferences preferences,
        CancellationToken ct)
    {
        var distanceKm = from.DistanceKmTo(to);
        TransportMode mode;

        if (distanceKm < 1.5)
        {
            // Within same zone: always walk+PT
            mode = TransportMode.WALK_AND_PUBLIC_TRANSPORT;
        }
        else if (preferences.CarAvailable)
        {
            var ptEstimate = await _transitCalculator.EstimateAsync(
                from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT, ct);
            var carEstimate = await _transitCalculator.EstimateAsync(
                from, to, TransportMode.CAR, ct);

            var ptSlowerBy = ptEstimate.DurationMinutes - carEstimate.DurationMinutes;

            if (ptSlowerBy >= TripPlanningConstants.CarFasterThresholdMinutes
                || distanceKm >= TripPlanningConstants.InterZoneThresholdKm)
            {
                mode = TransportMode.CAR;
            }
            else
            {
                mode = TransportMode.WALK_AND_PUBLIC_TRANSPORT;
            }
        }
        else
        {
            mode = TransportMode.WALK_AND_PUBLIC_TRANSPORT;
        }

        var estimate = await _transitCalculator.EstimateAsync(from, to, mode, ct);
        return new TransitDetails(mode, estimate.DurationMinutes, estimate.BufferMinutes, estimate.FrictionAlert);
    }

    private static double EstimateDistanceFromNearestActivity(
        Place place,
        List<ActivityNode> activities,
        DayPlan dayPlan)
    {
        // For MVP, return a nominal distance since previous-activity Place
        // locations aren't available on ActivityNode.
        // A future enhancement could embed location data on ActivityNode.
        if (activities.Count == 0)
            return 0;

        return 1.0;
    }

    private static ActivityNode CreateActivityNode(Place place, int sequenceOrder)
    {
        return new ActivityNode(
            place.Id,
            place.Name,
            sequenceOrder,
            place.TypicalDurationMinutes,
            place.IsIndoor,
            transitToNext: null,
            priority: Priority.Medium);
    }

    private static bool IsPlaceOpenOnDay(Place place, DayOfWeek dayOfWeek)
    {
        if (place.OpeningHours.Count == 0)
            return true; // No hours data — assume always open

        return place.OpeningHours.Any(oh => oh.IsOpenOn(dayOfWeek));
    }

    private static bool CanAddActivity(DayPlan dayPlan, BlockType blockType, int durationMinutes)
    {
        var block = dayPlan.GetBlock(blockType);
        return block.CanFitActivity(durationMinutes);
    }

    private static int GetTotalFreeSlots(DayPlan dayPlan)
    {
        return (TripPlanningConstants.MaxVisitsPerMorningBlock - dayPlan.Morning.Activities.Count)
             + (TripPlanningConstants.MaxVisitsPerAfternoonBlock - dayPlan.Afternoon.Activities.Count)
             + (TripPlanningConstants.MaxVisitsPerEveningBlock - dayPlan.Evening.Activities.Count);
    }

    private static int GetBlockMaxVisits(BlockType blockType) => blockType switch
    {
        BlockType.Morning => TripPlanningConstants.MaxVisitsPerMorningBlock,
        BlockType.Afternoon => TripPlanningConstants.MaxVisitsPerAfternoonBlock,
        BlockType.Evening => TripPlanningConstants.MaxVisitsPerEveningBlock,
        _ => 0
    };

    private static BlockType[] GetAdjacentBlocks(BlockType blockType) => blockType switch
    {
        BlockType.Morning => new[] { BlockType.Afternoon },
        BlockType.Afternoon => new[] { BlockType.Morning, BlockType.Evening },
        BlockType.Evening => new[] { BlockType.Afternoon },
        _ => Array.Empty<BlockType>()
    };
}
