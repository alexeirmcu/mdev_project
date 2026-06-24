namespace SmartTripPlanner.Domain.ApiModels;

public record PlaceModel(
    long PlaceId ,
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
