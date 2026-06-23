namespace SmartTripPlanner.Domain.ApiModels;

public record PlaceSearchRequest(
    string? Query,
    string CityCode,
    int? MaxResults,
    string? Category = null,
    bool? IsIndoor = null,
    bool? IsFamilyFriendly = null,
    int? MaxDurationMinutes = null);
