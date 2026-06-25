namespace SmartTripPlanner.Domain.ApiModels;

public record TripSummaryResponse(
    Guid TripId,
    long CityId,
    string CityCode,
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalMustSees,
    int CompletedActivitiesCount,
    int TotalActivitiesCount);
