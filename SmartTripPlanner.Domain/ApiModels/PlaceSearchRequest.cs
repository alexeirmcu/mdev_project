namespace SmartTripPlanner.Domain.ApiModels;

public record PlaceSearchRequest(
    string? Query,
    string CityCode,
    int? MaxResults);
