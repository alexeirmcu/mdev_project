namespace SmartTripPlanner.Domain.ApiModels;

public record TripGenerationRequest(
    string CityCode,
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel BaseHotel,
    IReadOnlyList<MustSeeInput> MustSees,
    TravelersInput? Travelers = null,
    TripPreferencesInput? Preferences = null,
    string DefaultStartHour = "09:00",
    bool GenerateItinerary = true);
