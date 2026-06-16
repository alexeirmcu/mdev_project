namespace SmartTripPlanner.Domain.Exceptions;

public class CityNotFoundException : SmartTripDomainException
{
    public CityNotFoundException(string cityCode)
        : base($"City with code '{cityCode}' was not found.") { }
}
