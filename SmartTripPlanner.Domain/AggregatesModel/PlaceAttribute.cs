using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class PlaceAttribute : Entity
{
    public string Provider { get; private set; }
    public string Key { get; private set; }
    public string Value { get; private set; }
    public string? ProviderId { get; private set; }

    private PlaceAttribute() { Provider = null!; Key = null!; Value = null!; }

    internal PlaceAttribute(long id, string provider, string key, string value, string? providerId = null)
        : this(provider, key, value, providerId)
    {
        Id = id;
    }

    public PlaceAttribute(string provider, string key, string value, string? providerId = null)
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

        ProviderId = providerId;
    }

    /// <summary>
    /// Updates the ProviderId. Only updates if the new value is non-null/non-empty
    /// so existing values are not overwritten with null.
    /// </summary>
    public void UpdateProviderId(string? providerId)
    {
        if (!string.IsNullOrEmpty(providerId))
            ProviderId = providerId;
    }
}
