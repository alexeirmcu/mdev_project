using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class City : Entity, IAggregateRoot
{
    public string CityCode { get; private set; } 
    public string CityName { get; private set; }

    public City(string cityCode, string cityName)
    {
        CityCode = cityCode;
        CityName = cityName;
    }
}
