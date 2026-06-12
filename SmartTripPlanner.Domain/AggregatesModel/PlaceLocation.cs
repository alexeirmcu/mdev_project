using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class PlaceLocation : ValueObject
{
    public double Latitude { get; }
    public double Longitude { get; }

    public PlaceLocation(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new SmartTripDomainException("Latitude must be between -90 and 90.");
        if (longitude < -180 || longitude > 180)
            throw new SmartTripDomainException("Longitude must be between -180 and 180.");

        Latitude = latitude;
        Longitude = longitude;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}
