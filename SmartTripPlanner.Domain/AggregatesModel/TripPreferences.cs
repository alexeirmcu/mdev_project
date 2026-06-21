using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class TripPreferences : ValueObject
{
    public bool CarAvailable { get; }
    public int MaxWalkingMinutes { get; }
    public bool WeatherAwareEnabled { get; }
    public ReturnToHotelStrategy ReturnToHotelStrategy { get; }
    public List<string> Interests { get; private set; } = new();

    private TripPreferences() { }

    public TripPreferences(bool carAvailable = false, int maxWalkingMinutes = 30, bool weatherAwareEnabled = true,
        IEnumerable<string>? interests = null,
        ReturnToHotelStrategy returnToHotelStrategy = ReturnToHotelStrategy.Always)
    {
        if (maxWalkingMinutes < 0)
            throw new ArgumentException("MaxWalkingMinutes cannot be negative.", nameof(maxWalkingMinutes));

        CarAvailable = carAvailable;
        MaxWalkingMinutes = maxWalkingMinutes;
        WeatherAwareEnabled = weatherAwareEnabled;
        ReturnToHotelStrategy = returnToHotelStrategy;
        if (interests is not null)
            Interests = interests.ToList();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CarAvailable;
        yield return MaxWalkingMinutes;
        yield return WeatherAwareEnabled;
        yield return ReturnToHotelStrategy;
        foreach (var interest in Interests)
            yield return interest;
    }
}
