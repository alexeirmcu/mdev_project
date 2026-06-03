using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class City : Entity, IAggregateRoot
{
    public string CityId { get; private set; }
    public string CityName { get; private set; }

    public City(string cityId, string cityName)
    {
        CityId = cityId;
        CityName = cityName;
    }
}
