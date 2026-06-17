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
    string DefaultStartHour);
