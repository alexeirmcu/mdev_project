using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.Ports;

public interface IPlaceExternalService
{
    Task<List<Place>> SearchPlacesAsync(string query, string cityCode, long cityId, int maxResults = 20);
}
