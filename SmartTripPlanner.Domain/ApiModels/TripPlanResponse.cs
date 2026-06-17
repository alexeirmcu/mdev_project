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

public class BlockResponse
{
    public string BlockType { get; set; } = string.Empty;
    public int TotalDurationMinutes { get; set; }
    public List<ActivityResponse> Activities { get; set; } = new();
}

public class ActivityResponse
{
    public string PlaceName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string TransportMode { get; set; } = string.Empty;
    public int TransitDurationMinutes { get; set; }
}
