using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class PlaceAttribute : ValueObject
{
    public string Provider { get; }
    public string Key { get; }
    public string Value { get; }

    private PlaceAttribute() { }

    public PlaceAttribute(string provider, string key, string value)
    {
        Provider = provider ?? throw new SmartTripDomainException("Provider cannot be null.");
        if (provider == string.Empty)
            throw new SmartTripDomainException("Provider cannot be empty.");

        Key = key ?? throw new SmartTripDomainException("Key cannot be null.");
        if (key == string.Empty)
            throw new SmartTripDomainException("Key cannot be empty.");

        Value = value ?? throw new SmartTripDomainException("Value cannot be null.");
        if (value == string.Empty)
            throw new SmartTripDomainException("Value cannot be empty.");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Provider;
        yield return Key;
        yield return Value;
    }
}
