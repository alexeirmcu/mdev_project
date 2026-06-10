using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Domain.Ports;

public interface IPlaceExternalService
{
    Task<List<Place>> SearchPlacesAsync(string query, string cityId, int maxResults = 20);
}
