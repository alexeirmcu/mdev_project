using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class SelectedAttraction : ValueObject
{
    public string PlaceId { get; }
    public string Name { get; }

    public SelectedAttraction(string placeId, string name)
    {
        PlaceId = placeId ?? throw new ArgumentNullException(nameof(placeId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PlaceId;
    }
}
