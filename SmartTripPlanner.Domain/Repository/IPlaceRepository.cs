using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityCode, int maxResults = 20);
    Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId);
    Task<IEnumerable<Place>> GetManyByIdsAsync(IEnumerable<long> placeIds, CancellationToken ct);
    Task<List<Place>> GetManyByCityIdAsync(long cityId, IEnumerable<string>? interests = null, CancellationToken ct = default);
    Task<List<string>> GetDistinctInterestsByCityIdAsync(long cityId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Place> places);
    Task UpsertRangeAsync(IEnumerable<Place> places);
}
