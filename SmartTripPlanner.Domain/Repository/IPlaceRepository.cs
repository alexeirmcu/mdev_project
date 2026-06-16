using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityCode, int maxResults = 20);
    Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId);
    Task AddRangeAsync(IEnumerable<Place> places);
    Task UpsertRangeAsync(IEnumerable<Place> places);
}
