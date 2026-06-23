namespace SmartTripPlanner.Domain.Exceptions;

public class TripForbiddenException : SmartTripDomainException
{
    public TripForbiddenException(Guid tripId, string caller)
        : base($"Trip '{tripId}' does not belong to caller '{caller}'.") { }
}
