using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityId, int maxResults = 20);
    Task<Place?> GetByPlaceIdAsync(string placeId);
}
