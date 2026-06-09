using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Place : Entity, IAggregateRoot
{
    public string PlaceId { get; private set; }
    public string Name { get; private set; }
    public string CityId { get; private set; }
    public PlaceLocation Location { get; private set; }
    public int TypicalDurationMinutes { get; private set; } = 60;
    public bool IsIndoor { get; private set; } = false;
    public bool IsFamilyFriendly { get; private set; } = true;
    public List<OpeningHoursWindow> OpeningHours { get; private set; } = new();

    private Place() { PlaceId = null!; Name = null!; CityId = null!; Location = null!; }

    public Place(string placeId, string name, string cityId, PlaceLocation location)
    {
        PlaceId = placeId ?? throw new ArgumentNullException(nameof(placeId));
        if (placeId == string.Empty)
            throw new ArgumentException("PlaceId cannot be empty", nameof(placeId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        if (name == string.Empty)
            throw new ArgumentException("Name cannot be empty", nameof(name));
        CityId = cityId ?? throw new ArgumentNullException(nameof(cityId));
        Location = location ?? throw new ArgumentNullException(nameof(location));
    }
}
