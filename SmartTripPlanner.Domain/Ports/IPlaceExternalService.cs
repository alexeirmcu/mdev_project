using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.Domain.Ports;

public interface IPlaceExternalService
{
    Task<List<Place>> SearchPlacesAsync(string query, string cityCode, long cityId, int maxResults = 20,
        PlaceSearchFilter? filter = null, List<string>? fsqCategoryIds = null);
}
