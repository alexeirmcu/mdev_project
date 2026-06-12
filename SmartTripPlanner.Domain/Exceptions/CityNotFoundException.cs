namespace SmartTripPlanner.Domain.Exceptions;

public class CityNotFoundException : SmartTripDomainException
{
    public CityNotFoundException(string cityId)
        : base($"City with id '{cityId}' was not found.") { }
}
