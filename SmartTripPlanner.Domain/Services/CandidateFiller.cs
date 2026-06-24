using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Fills remaining block capacity with scored candidate places using real Haversine distance.
/// </summary>
public class CandidateFiller : ICandidateFiller
{
    private readonly ICandidateScorer _scorer;

    public CandidateFiller(ICandidateScorer scorer)
    {
        _scorer = scorer;
    }

    public async Task FillAsync(Trip trip, List<Place> candidatePool, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
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
                var remainingCapacity = ItineraryGeneratorHelpers.GetBlockMaxVisits(blockType) - block.Activities.Count;

                if (remainingCapacity <= 0)
                    continue;

                var availableCandidates = candidatePool
                    .Where(p => !usedPlaceIds.Contains(p.Id)
                                && ItineraryGeneratorHelpers.IsPlaceOpenOnDay(p, dayPlan.Date.DayOfWeek))
                    .ToList();

                if (availableCandidates.Count == 0)
                    continue;

                // Score and rank candidates using real Haversine distance
                var scored = availableCandidates
                    .Select(p =>
                    {
                        var distance = EstimateDistanceFromNearestActivity(p, block.Activities);
                        var context = new ScoringContext(
                            IsFamilyTrip: trip.Travelers.Children > 0,
                            IsBadWeather: isBadWeather && trip.Preferences.WeatherAwareEnabled,
                            DistanceFromBlockCenterKm: distance,
                            PopularityRaw: p.Popularity);
                        var score = _scorer.Score(p, context);
                        return (Place: p, Score: score);
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                foreach (var (place, _) in scored)
                {
                    if (remainingCapacity <= 0)
                        break;

                    if (!ItineraryGeneratorHelpers.CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                        continue;

                    var activity = ItineraryGeneratorHelpers.CreateActivityNode(place, block.Activities.Count + 1);
                    dayPlan.AddActivity(blockType, activity);
                    usedPlaceIds.Add(place.Id);
                    remainingCapacity--;
                }
            }
        }

        await Task.CompletedTask;
    }

    public async Task FillScopedAsync(Trip trip, ReplanScope scope, List<Place> candidatePool, HashSet<long> excludePlaceIds, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
    {
        if (candidatePool.Count == 0)
            return;

        var usedPlaceIds = new HashSet<long>(trip.OriginalMustSees.Select(m => m.PlaceId));
        foreach (var id in excludePlaceIds)
            usedPlaceIds.Add(id);

        foreach (var dayPlan in trip.Days)
        {
            var isBadWeather = weatherData.TryGetValue(dayPlan.Date, out var weather)
                               && weather == WeatherCondition.Bad;

            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = dayPlan.GetBlock(blockType);
                var remainingCapacity = ItineraryGeneratorHelpers.GetBlockMaxVisits(blockType) - block.Activities.Count;

                if (remainingCapacity <= 0)
                    continue;

                var availableCandidates = candidatePool
                    .Where(p => !usedPlaceIds.Contains(p.Id)
                                && ItineraryGeneratorHelpers.IsPlaceOpenOnDay(p, dayPlan.Date.DayOfWeek))
                    .ToList();

                if (availableCandidates.Count == 0)
                    continue;

                var scored = availableCandidates
                    .Select(p =>
                    {
                        var distance = EstimateDistanceFromNearestActivity(p, block.Activities);
                        var context = new ScoringContext(
                            IsFamilyTrip: trip.Travelers.Children > 0,
                            IsBadWeather: isBadWeather && trip.Preferences.WeatherAwareEnabled,
                            DistanceFromBlockCenterKm: distance,
                            PopularityRaw: p.Popularity);
                        var score = _scorer.Score(p, context);
                        return (Place: p, Score: score);
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                foreach (var (place, _) in scored)
                {
                    if (remainingCapacity <= 0)
                        break;

                    if (!ItineraryGeneratorHelpers.CanAddActivity(dayPlan, blockType, place.TypicalDurationMinutes))
                        continue;

                    var activity = ItineraryGeneratorHelpers.CreateActivityNode(place, block.Activities.Count + 1);
                    dayPlan.AddActivity(blockType, activity);
                    usedPlaceIds.Add(place.Id);
                    remainingCapacity--;
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Computes the Haversine distance from a candidate place to the nearest
    /// existing activity in the block. Uses ActivityNode.Location which was
    /// populated at creation time.
    /// </summary>
    private static double EstimateDistanceFromNearestActivity(Place place, List<ActivityNode> activities)
    {
        if (activities.Count == 0)
            return 0;

        return activities.Min(a => a.Location.DistanceKmTo(place.Location));
    }
}
