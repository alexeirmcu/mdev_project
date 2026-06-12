namespace SmartTripPlanner.Domain.Exceptions;

public class OverConstrainedRouteException : SmartTripDomainException
{
    public IReadOnlyList<string> ConflictingPlaceIds { get; }

    public OverConstrainedRouteException(IReadOnlyList<string> conflictingPlaceIds)
        : base("The route is over-constrained. Not all must-sees can be accommodated.")
    {
        ConflictingPlaceIds = conflictingPlaceIds;
    }
}
