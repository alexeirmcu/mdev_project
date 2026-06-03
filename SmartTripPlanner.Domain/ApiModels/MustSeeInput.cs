using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.ApiModels;

public record MustSeeInput(
    string PlaceId,
    Priority Priority,
    int? PinnedDayIndex = null,
    BlockType? PinnedBlock = null);
