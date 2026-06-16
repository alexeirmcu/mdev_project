namespace SmartTripPlanner.Domain.ApiModels;

public record PlaceModel(
    string ProviderReferenceId,
    string Name,
    long CityId,
    PlaceLocationModel Location,
    int TypicalDurationMinutes,
    bool IsIndoor,
    bool IsFamilyFriendly,
    bool IsAutoUpdateEnabled,
    IReadOnlyList<OpeningHoursWindowModel> OpeningHours,
    IReadOnlyList<PlaceAttributeModel> Attributes);
