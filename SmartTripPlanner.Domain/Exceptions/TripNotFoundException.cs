namespace SmartTripPlanner.Domain.Exceptions;

public class TripNotFoundException : Exception
{
    public TripNotFoundException(Guid tripId)
        : base($"Trip with id '{tripId}' was not found.") { }
}
