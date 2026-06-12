using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Place : Entity, IAggregateRoot
{
    public string PlaceId { get; private set; }
    public string Name { get; private set; }
    public string CityId { get; private set; }
    public City? City { get; private set; }
    public PlaceLocation Location { get; private set; }
    public int TypicalDurationMinutes { get; private set; } = 60;
    public bool IsIndoor { get; private set; } = false;
    public bool IsFamilyFriendly { get; private set; } = true;
    public List<OpeningHoursWindow> OpeningHours { get; private set; } = new();

    private Place() { PlaceId = null!; Name = null!; CityId = null!; Location = null!; }

    public Place(string placeId, string name, string cityId, PlaceLocation location)
    {
        PlaceId = placeId ?? throw new SmartTripDomainException("PlaceId cannot be null.");
        if (placeId == string.Empty)
            throw new SmartTripDomainException("PlaceId cannot be empty.");
        Name = name ?? throw new SmartTripDomainException("Name cannot be null.");
        if (name == string.Empty)
            throw new SmartTripDomainException("Name cannot be empty.");
        CityId = cityId ?? throw new SmartTripDomainException("CityId cannot be null.");
        Location = location ?? throw new SmartTripDomainException("Location cannot be null.");
    }

    public Place(string placeId, string name, string cityId, PlaceLocation location,
                 int typicalDurationMinutes, bool isIndoor, bool isFamilyFriendly)
        : this(placeId, name, cityId, location)
    {
        TypicalDurationMinutes = typicalDurationMinutes;
        IsIndoor = isIndoor;
        IsFamilyFriendly = isFamilyFriendly;
    }
}
