namespace SmartTripPlanner.Domain.ApiModels;

public record TripGenerationRequest(
    string CityCode,
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel? BaseHotel = null,
    IReadOnlyList<MustSeeInput> MustSees = null,
    TravelersInput? Travelers = null,
    TripPreferencesInput? Preferences = null,
    string DefaultStartHour = "09:00");
