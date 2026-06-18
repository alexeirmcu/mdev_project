namespace SmartTripPlanner.Domain.ApiModels;

public record TripUpdateRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    LocationModel? BaseHotel = null,
    TravelersInput? Travelers = null,
    TripPreferencesInput? Preferences = null,
    string? DefaultStartHour = null,
    List<MustSeeInput>? MustSeesToAdd = null,
    List<long>? MustSeesToRemove = null,
    bool GenerateItinerary = true);
