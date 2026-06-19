using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class TripPreferences : ValueObject
{
    public bool CarAvailable { get; }
    public int MaxWalkingMinutes { get; }
    public bool WeatherAwareEnabled { get; }
    public List<string> Interests { get; private set; } = new();

    private TripPreferences() { }

    public TripPreferences(bool carAvailable = false, int maxWalkingMinutes = 30, bool weatherAwareEnabled = true,
        IEnumerable<string>? interests = null)
    {
        if (maxWalkingMinutes < 0)
            throw new ArgumentException("MaxWalkingMinutes cannot be negative.", nameof(maxWalkingMinutes));

        CarAvailable = carAvailable;
        MaxWalkingMinutes = maxWalkingMinutes;
        WeatherAwareEnabled = weatherAwareEnabled;
        if (interests is not null)
            Interests = interests.ToList();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CarAvailable;
        yield return MaxWalkingMinutes;
        yield return WeatherAwareEnabled;
        foreach (var interest in Interests)
            yield return interest;
    }
}
