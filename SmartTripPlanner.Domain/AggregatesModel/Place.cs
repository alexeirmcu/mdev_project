using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Place : Entity, IAggregateRoot
{
    public string ProviderReferenceId { get; private set; }
    public Provider Provider { get; private set; }
    public string Name { get; private set; }
    public long CityId { get; private set; }
    public City? City { get; private set; }
    public PlaceLocation Location { get; private set; }
    public int TypicalDurationMinutes { get; private set; } = 60;
    public bool IsIndoor { get; private set; } = false;
    public bool IsFamilyFriendly { get; private set; } = true;
    public bool IsAutoUpdateEnabled { get; private set; } = true;
    public int FamilyFriendlyScore { get; private set; } = 3;
    public double Popularity { get; private set; } = 0.5;
    public bool IsEnriched { get; private set; } = false;
    public List<OpeningHoursWindow> OpeningHours { get; private set; } = new();
    public ICollection<PlaceAttribute> Attributes { get; private set; } = new List<PlaceAttribute>();

    private Place() { ProviderReferenceId = null!; Name = null!; Location = null!; }

    public Place(string providerReferenceId, string name, long cityId, PlaceLocation location,
                 Provider provider = Provider.Foursquare)
    {
        ProviderReferenceId = providerReferenceId ?? throw new SmartTripDomainException("ProviderReferenceId cannot be null.");
        if (providerReferenceId == string.Empty)
            throw new SmartTripDomainException("ProviderReferenceId cannot be empty.");
        Provider = provider;
        Name = name ?? throw new SmartTripDomainException("Name cannot be null.");
        if (name == string.Empty)
            throw new SmartTripDomainException("Name cannot be empty.");
        CityId = cityId;
        Location = location ?? throw new SmartTripDomainException("Location cannot be null.");
    }

    public Place(string providerReferenceId, string name, long cityId, PlaceLocation location,
                 int typicalDurationMinutes, bool isIndoor, bool isFamilyFriendly,
                 Provider provider = Provider.Foursquare)
        : this(providerReferenceId, name, cityId, location, provider)
    {
        TypicalDurationMinutes = typicalDurationMinutes;
        IsIndoor = isIndoor;
        IsFamilyFriendly = isFamilyFriendly;
    }

    public Place(string providerReferenceId, string name, long cityId, PlaceLocation location,
                 int typicalDurationMinutes, bool isIndoor, bool isFamilyFriendly, bool isAutoUpdateEnabled,
                 Provider provider = Provider.Foursquare)
        : this(providerReferenceId, name, cityId, location, typicalDurationMinutes, isIndoor, isFamilyFriendly, provider)
    {
        IsAutoUpdateEnabled = isAutoUpdateEnabled;
    }

    public void AddAttribute(PlaceAttribute attribute)
    {
        Attributes.Add(attribute ?? throw new SmartTripDomainException("Attribute cannot be null."));
    }

    public void MarkEnriched(int typicalDurationMinutes, bool isIndoor, int familyFriendlyScore, double popularity)
    {
        if (familyFriendlyScore < 1 || familyFriendlyScore > 5)
            throw new SmartTripDomainException("FamilyFriendlyScore must be between 1 and 5.");
        if (popularity < 0.0 || popularity > 1.0)
            throw new SmartTripDomainException("Popularity must be between 0.0 and 1.0.");
        if (typicalDurationMinutes <= 0)
            throw new SmartTripDomainException("TypicalDurationMinutes must be greater than 0.");

        TypicalDurationMinutes = typicalDurationMinutes;
        IsIndoor = isIndoor;
        FamilyFriendlyScore = familyFriendlyScore;
        Popularity = popularity;
        IsEnriched = true;
    }

    public void UpdateFromExternalProvider(string name, PlaceLocation location,
        int typicalDurationMinutes, bool isIndoor, bool isFamilyFriendly,
        ICollection<PlaceAttribute> attributes)
    {
        if (!IsAutoUpdateEnabled)
            throw new InvalidOperationException("Cannot update a place with auto-update disabled.");

        Name = name;
        Location = location;
        TypicalDurationMinutes = typicalDurationMinutes;
        IsIndoor = isIndoor;
        IsFamilyFriendly = isFamilyFriendly;

        Attributes.Clear();
        foreach (var attr in attributes)
            Attributes.Add(attr);
    }
}
