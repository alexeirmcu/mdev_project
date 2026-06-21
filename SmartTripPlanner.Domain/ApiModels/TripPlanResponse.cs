namespace SmartTripPlanner.Domain.ApiModels;

public record TripPlanResponse(
    Guid TripId,
    string TripCode,
    long CityId,
    string CityCode,
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel BaseHotel,
    TravelersInput Travelers,
    TripPreferencesInput Preferences,
    IReadOnlyList<MustSeeResponse> MustSees,
    string Status,
    string DefaultStartHour)
{
    public List<DayPlanResponse> Days { get; init; } = new();
}

public class DayPlanResponse
{
    public int DayIndex { get; set; }
    public DateOnly Date { get; set; }
    public string WeatherSummary { get; set; } = string.Empty;
    public List<BlockResponse> Blocks { get; set; } = new();
}

public record TransitResponse(
    string TransportMode,
    int DurationMinutes,
    int BufferMinutes,
    bool FrictionAlert);

public class BlockResponse
{
    public string BlockType { get; set; } = string.Empty;
    public int TotalDurationMinutes { get; set; }
    public TransitResponse? TransitFromHotel { get; set; }
    public TransitResponse? TransitToHotel { get; set; }
    public TransitResponse? InterBlockTransit { get; set; }
    public List<ActivityResponse> Activities { get; set; } = new();
}

public class ActivityResponse
{
    public long PlaceId { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsIndoor { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string TransportMode { get; set; } = string.Empty;
    public int TransitDurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public bool FrictionAlert { get; set; }
    public int? EstimatedArrival { get; set; }
    public int? EstimatedDeparture { get; set; }
}
