using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Core heuristic itinerary generator implementing a 5-phase algorithm.
/// Now a thin orchestrator that delegates to injectable collaborator services.
/// </summary>
public class HeuristicItineraryGenerator : IItineraryGenerator
{
    private readonly IPinnedMustSeePlacer _pinnedPlacer;
    private readonly IUnpinnedMustSeePlacer _unpinnedPlacer;
    private readonly ICandidateFiller _candidateFiller;
    private readonly ITransitEnricher _transitEnricher;
    private readonly ITimelineScheduler _timelineScheduler;

    public HeuristicItineraryGenerator(
        IPinnedMustSeePlacer pinnedPlacer,
        IUnpinnedMustSeePlacer unpinnedPlacer,
        ICandidateFiller candidateFiller,
        ITransitEnricher transitEnricher,
        ITimelineScheduler timelineScheduler)
    {
        _pinnedPlacer = pinnedPlacer;
        _unpinnedPlacer = unpinnedPlacer;
        _candidateFiller = candidateFiller;
        _transitEnricher = transitEnricher;
        _timelineScheduler = timelineScheduler;
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
        var placesById = allPlaces.ToDictionary(p => p.Id);
        var mustSeeIds = new HashSet<long>(trip.OriginalMustSees.Select(m => m.PlaceId));
        var mustSeeEntries = trip.OriginalMustSees
            .Where(m => placesById.ContainsKey(m.PlaceId))
            .Select(m => (MustSee: m, Place: placesById[m.PlaceId]))
            .ToList();
        var candidatePool = allPlaces.Where(p => !mustSeeIds.Contains(p.Id)).ToList();

        var unplacedHigh = new List<long>();

        // Phase 2: Place pinned must-sees (exact day/block)
        foreach (var (mustSee, place) in mustSeeEntries.Where(e => e.MustSee.PinnedDayIndex.HasValue))
        {
            if (!_pinnedPlacer.Place(trip, mustSee, place) && mustSee.Priority == Priority.High)
                unplacedHigh.Add(mustSee.PlaceId);
        }

        // Phase 3: Place unpinned must-sees using zone clustering
        var unpinnedEntries = mustSeeEntries
            .Where(e => !e.MustSee.PinnedDayIndex.HasValue && !unplacedHigh.Contains(e.MustSee.PlaceId))
            .ToList();

        var clusters = ZoneClusteringHelper.Cluster(unpinnedEntries.Select(e => e.Place).ToList());
        foreach (var cluster in clusters)
        {
            var clusterEntries = unpinnedEntries
                .Where(e => cluster.Any(p => p.Id == e.Place.Id))
                .OrderByDescending(e => e.MustSee.Priority);

            foreach (var (mustSee, place) in clusterEntries)
            {
                if (!_unpinnedPlacer.Place(trip, mustSee, place) && mustSee.Priority == Priority.High)
                    unplacedHigh.Add(mustSee.PlaceId);
            }
        }

        // Phase 4: Fill remaining block capacity with scored candidates
        await _candidateFiller.FillAsync(trip, candidatePool, weatherData, ct);

        // Phase 5: Enrich with transit estimates and weather
        await _transitEnricher.EnrichAsync(trip, placesById, weatherData, ct);

        // Phase 6: Compute wall-clock arrival/departure times
        _timelineScheduler.Schedule(trip);

        // Fallback chain: if High must-sees remain unplaced, throw
        if (unplacedHigh.Count > 0)
        {
            throw new OverConstrainedRouteException(unplacedHigh.AsReadOnly());
        }
    }
}
