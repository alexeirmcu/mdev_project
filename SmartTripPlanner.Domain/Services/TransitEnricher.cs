using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Enriches trip days with transit estimates between consecutive activities
/// and weather summary per day. Uses ActivityNode.Location directly instead
/// of dictionary lookups.
/// </summary>
public class TransitEnricher : ITransitEnricher
{
    private readonly ITransitCalculator _transitCalculator;

    public TransitEnricher(ITransitCalculator transitCalculator)
    {
        _transitCalculator = transitCalculator;
    }

    public async Task EnrichAsync(Trip trip, IReadOnlyDictionary<long, Place> placesById, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
    {
        foreach (var dayPlan in trip.Days)
        {
            // Set weather summary
            if (weatherData.TryGetValue(dayPlan.Date, out var weather))
                dayPlan.SetWeather(weather);

            // Calculate transit between consecutive activities in each block
            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = dayPlan.GetBlock(blockType);
                var activities = block.Activities;
                for (int i = 0; i < activities.Count; i++)
                {
                    if (i < activities.Count - 1)
                    {
                        var fromActivity = activities[i];
                        var toActivity = activities[i + 1];

                        // Use ActivityNode.Location directly (populated at creation time)
                        if (fromActivity.Location is not null && toActivity.Location is not null)
                        {
                            var transit = await AssignTransitAsync(
                                fromActivity.Location,
                                toActivity.Location,
                                trip.Preferences,
                                ct);

                            fromActivity.TransitToNext = transit;
                        }
                    }
                }

                // Hotel transit: hotel → first activity, last activity → hotel
                if (trip.BaseHotel is not null && activities.Count > 0)
                {
                    var hotelLocation = new PlaceLocation(trip.BaseHotel.Latitude, trip.BaseHotel.Longitude);

                    // Transit from hotel to first activity
                    if (activities[0].Location is not null)
                    {
                        block.TransitFromHotel = await AssignTransitAsync(
                            hotelLocation,
                            activities[0].Location,
                            trip.Preferences,
                            ct);
                    }

                    // Transit from last activity to hotel
                    if (activities[^1].Location is not null)
                    {
                        block.TransitToHotel = await AssignTransitAsync(
                            activities[^1].Location,
                            hotelLocation,
                            trip.Preferences,
                            ct);
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
}
