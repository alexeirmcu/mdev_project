namespace SmartTripPlanner.Domain.ApiModels;

public record MustSeeResponse(
    long PlaceId,
    string Priority,
    int? PinnedDayIndex,
    string? PinnedBlock,
    bool ForceIncludeDespiteWeather = false);
