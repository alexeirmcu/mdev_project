namespace SmartTripPlanner.Domain.ApiModels;

public record TripSummaryResponse(
    Guid TripId,
    string CityId,
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalMustSees,
    int CompletedActivitiesCount,
    int TotalActivitiesCount);
