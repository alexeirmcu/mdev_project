namespace SmartTripPlanner.Domain.ApiModels;

public record PlaceModel(
    string PlaceId,
    string Name,
    string CityId,
    PlaceLocationModel Location,
    int TypicalDurationMinutes,
    bool IsIndoor,
    bool IsFamilyFriendly,
    IReadOnlyList<OpeningHoursWindowModel> OpeningHours);
