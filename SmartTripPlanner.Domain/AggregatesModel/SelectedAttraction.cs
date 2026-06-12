using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class SelectedAttraction : ValueObject
{
    public string PlaceId { get; }
    public string Name { get; }

    public SelectedAttraction(string placeId, string name)
    {
        PlaceId = placeId ?? throw new SmartTripDomainException("PlaceId cannot be null.");
        Name = name ?? throw new SmartTripDomainException("Name cannot be null.");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PlaceId;
    }
}
