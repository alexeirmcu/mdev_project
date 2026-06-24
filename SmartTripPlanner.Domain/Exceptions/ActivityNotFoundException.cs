namespace SmartTripPlanner.Domain.Exceptions;

public class ActivityNotFoundException : SmartTripDomainException
{
    public ActivityNotFoundException(long placeId)
        : base($"Activity with place id '{placeId}' was not found.") { }
}
