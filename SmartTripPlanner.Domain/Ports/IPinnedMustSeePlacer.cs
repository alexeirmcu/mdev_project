using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Places a pinned must-see at its exact day/block.
/// Returns true if the must-see was successfully placed.
/// </summary>
public interface IPinnedMustSeePlacer
{
    bool Place(Trip trip, MustSee mustSee, Place place);
}
