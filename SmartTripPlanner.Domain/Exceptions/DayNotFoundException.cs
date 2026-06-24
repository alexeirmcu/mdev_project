namespace SmartTripPlanner.Domain.Exceptions;

public class DayNotFoundException : SmartTripDomainException
{
    public DayNotFoundException(int dayIndex)
        : base($"Day with index '{dayIndex}' was not found.") { }

    public DayNotFoundException(Guid tripId, int dayIndex)
        : base($"Day with index '{dayIndex}' was not found in trip '{tripId}'.") { }
}
