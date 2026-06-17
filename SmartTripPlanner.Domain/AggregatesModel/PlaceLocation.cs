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

    public double DistanceKmTo(PlaceLocation other)
    {
        const double EarthRadiusKm = 6371.0;
        var dLat = ToRadians(other.Latitude - Latitude);
        var dLon = ToRadians(other.Longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;

        static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}
