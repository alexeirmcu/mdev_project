using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Domain port for estimating transit duration between two locations.
/// Implementations may use haversine heuristics or real routing APIs.
/// </summary>
public interface ITransitCalculator
{
    Task<TransitEstimate> EstimateAsync(
        PlaceLocation from,
        PlaceLocation to,
        TransportMode mode,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a transit estimation between two locations.
/// </summary>
public record TransitEstimate(
    int DurationMinutes,
    int BufferMinutes = 10,
    bool FrictionAlert = false);
