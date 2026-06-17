using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Infrastructure.Services;

/// <summary>
/// Estimates transit duration and mode between two locations using
/// haversine distance and mode-specific speed constants.
/// This is an MVP heuristic — no real routing API is called.
/// </summary>
public class HaversineTransitCalculator : ITransitCalculator
{
    public Task<TransitEstimate> EstimateAsync(
        PlaceLocation from,
        PlaceLocation to,
        TransportMode mode,
        CancellationToken ct = default)
    {
        var distanceKm = from.DistanceKmTo(to);

        var (speedKmh, bufferMinutes) = mode switch
        {
            TransportMode.WALK_AND_PUBLIC_TRANSPORT => (TripPlanningConstants.PublicTransportSpeedKmh, 10),
            TransportMode.CAR => (TripPlanningConstants.CarSpeedKmh, 5),
            _ => (TripPlanningConstants.PublicTransportSpeedKmh, 10)
        };

        // Duration in minutes = (distance / speed) * 60
        var durationMinutes = (int)Math.Ceiling((distanceKm / speedKmh) * 60.0);

        // Friction alert for long walking distances or very long transits
        var frictionAlert = mode == TransportMode.WALK_AND_PUBLIC_TRANSPORT
            && distanceKm > 2.0;

        // Ensure minimum duration of 2 minutes
        if (durationMinutes < 2)
            durationMinutes = 2;

        return Task.FromResult(new TransitEstimate(durationMinutes, bufferMinutes, frictionAlert));
    }
}
