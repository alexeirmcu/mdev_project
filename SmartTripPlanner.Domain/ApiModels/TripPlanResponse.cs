using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.ApiModels;

public record TripPlanResponse(
    Guid TripId,
    string CityId,
    IReadOnlyList<DayPlan> Days);
