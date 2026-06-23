namespace SmartTripPlanner.Domain.ApiModels;

public record PlaceSearchFilter(
    string? Category,
    bool? IsIndoor,
    bool? IsFamilyFriendly,
    int? MaxDurationMinutes);

// marker