using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.Ports;

/// <summary>
/// Places an unpinned must-see into the best available day/block.
/// Returns true if the must-see was successfully placed.
/// </summary>
public interface IUnpinnedMustSeePlacer
{
    bool Place(Trip trip, MustSee mustSee, Place place);
}
