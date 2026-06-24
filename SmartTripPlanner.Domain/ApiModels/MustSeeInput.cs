using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.ApiModels;

public record MustSeeInput(
    long PlaceId,
    Priority Priority,
    int? PinnedDayIndex = null,
    BlockType? PinnedBlock = null,
    bool ForceIncludeDespiteWeather = false);
