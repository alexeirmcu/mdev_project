using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Scores candidate places using the heuristic formula:
/// family-friendly bonus + popularity − distance penalty + weather adjustment.
/// </summary>
public class CandidateScorer : ICandidateScorer
{
    public double Score(Place place, ScoringContext context)
    {
        double score = 0;

        // Family-friendly bonus: when the trip includes children, prefer family-friendly places
        if (context.IsFamilyTrip && place.IsFamilyFriendly)
            score += TripPlanningConstants.FamilyFriendlyBonus;

        // Popularity contribution (0–20 range given PopularityRaw 0.0–1.0)
        score += context.PopularityRaw * TripPlanningConstants.PopularityWeight;

        // Distance penalty: farther from block center is worse
        score -= context.DistanceFromBlockCenterKm * TripPlanningConstants.DistancePenaltyWeight;

        // Weather adjustment: prefer indoor on bad weather, penalize outdoor
        // When forceIncludeDespiteWeather is set for outdoor places, skip both penalty and bonus
        if (context.IsBadWeather)
        {
            if (context.ForceIncludeDespiteWeather && !place.IsIndoor)
            {
                // Forced outdoor place: no penalty, no indoor bonus
            }
            else
            {
                score += place.IsIndoor
                    ? TripPlanningConstants.IndoorWeatherBonus
                    : TripPlanningConstants.OutdoorWeatherPenalty;
            }
        }

        return score;
    }
}
