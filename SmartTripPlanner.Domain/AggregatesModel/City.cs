using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class City : Entity, IAggregateRoot
{
    public string CityCode { get; private set; } 
    public string CityName { get; private set; }
    public bool IsAllowed { get; private set; }

    public City(string cityCode, string cityName, bool isAllowed = true)
    {
        CityCode = cityCode;
        CityName = cityName;
        IsAllowed = isAllowed;
    }

    public ICollection<Place> Places { get; private set; } = new List<Place>();
}
