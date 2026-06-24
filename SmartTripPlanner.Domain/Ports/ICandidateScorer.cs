using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Domain port for scoring candidate places during itinerary generation.
/// Higher scores indicate better candidates for a given block context.
/// </summary>
public interface ICandidateScorer
{
    double Score(Place place, ScoringContext context);
}

/// <summary>
/// Context data provided to the scorer for computing a place's suitability.
/// </summary>
public record ScoringContext(
    bool IsFamilyTrip,
    bool IsBadWeather,
    double DistanceFromBlockCenterKm,
    double PopularityRaw = 0.5,
    bool ForceIncludeDespiteWeather = false);
