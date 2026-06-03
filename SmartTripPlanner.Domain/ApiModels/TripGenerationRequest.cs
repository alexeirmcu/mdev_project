using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.ApiModels;

public record TripGenerationRequest(
    string CityId,
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel BaseHotel,
    IReadOnlyList<MustSeeInput> MustSees,
    string DefaultStartHour = "09:00");
