namespace SmartTripPlanner.Domain.Constants;

public static class TripPlanningConstants
{
    // Block durations in minutes
    public const int MorningBlockDurationMinutes = 210;    // ~3.5h
    public const int AfternoonBlockDurationMinutes = 180;  // ~3h
    public const int EveningBlockDurationMinutes = 105;    // ~1.75h

    // Max visits per block
    public const int MaxVisitsPerMorningBlock = 3;
    public const int MaxVisitsPerAfternoonBlock = 3;
    public const int MaxVisitsPerEveningBlock = 2;

    // Buffers in minutes
    public const int DefaultTransitBufferMinutes = 10;
    public const int DefaultActivityBufferMinutes = 15;

    // Default start hour (minutes from midnight): 09:00 = 540
    public const int DefaultStartHourMinutes = 540;

    // Zone clustering
    public const double ZoneRadiusKm = 2.0;

    // Transport mode selection
    public const int CarFasterThresholdMinutes = 20;
    public const double InterZoneThresholdKm = 10.0;

    // Transit speed constants (km/h)
    public const double WalkingSpeedKmh = 5.0;
    public const double PublicTransportSpeedKmh = 15.0;
    public const double CarSpeedKmh = 30.0;

    // Scoring constants
    public const double FamilyFriendlyBonus = 15.0;
    public const double PopularityWeight = 20.0;
    public const double DistancePenaltyWeight = 5.0;
    public const double IndoorWeatherBonus = 20.0;
    public const double OutdoorWeatherPenalty = -20.0;

    // Candidate selection
    public const int MaxCandidatesPerCity = 50;

    // Attribute keys
    public const string InterestAttributeKey = "category";
}
