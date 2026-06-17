namespace SmartTripPlanner.Domain.Exceptions;

public class OverConstrainedRouteException : SmartTripDomainException
{
    public IReadOnlyList<long> ConflictingPlaceIds { get; }

    public OverConstrainedRouteException(IReadOnlyList<long> conflictingPlaceIds)
        : base("The route is over-constrained. Not all must-sees can be accommodated.")
    {
        ConflictingPlaceIds = conflictingPlaceIds;
    }
}
