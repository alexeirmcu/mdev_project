using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class TripPreferences : ValueObject
{
    public bool CarAvailable { get; }
    public int MaxWalkingMinutes { get; }
    public bool WeatherAwareEnabled { get; }

    public TripPreferences(bool carAvailable = false, int maxWalkingMinutes = 30, bool weatherAwareEnabled = true)
    {
        if (maxWalkingMinutes < 0)
            throw new ArgumentException("MaxWalkingMinutes cannot be negative.", nameof(maxWalkingMinutes));

        CarAvailable = carAvailable;
        MaxWalkingMinutes = maxWalkingMinutes;
        WeatherAwareEnabled = weatherAwareEnabled;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CarAvailable;
        yield return MaxWalkingMinutes;
        yield return WeatherAwareEnabled;
    }
}
