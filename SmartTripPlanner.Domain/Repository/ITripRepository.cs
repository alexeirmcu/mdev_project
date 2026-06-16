using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Repository;

public interface ITripRepository : IRepository<Trip>
{
    Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken ct);
    Task<Trip?> GetByTripCodeAsync(string tripCode, CancellationToken ct);
    Task<bool> ExistsByTripCodeAsync(string tripCode, CancellationToken ct);
    Task<IEnumerable<Trip>> ListAsync(long? cityId, DateOnly? startDate, DateOnly? endDate, CancellationToken ct);
    Task AddAsync(Trip trip, CancellationToken ct);
    Task UpdateAsync(Trip trip, CancellationToken ct);
    Task DeleteAsync(Guid tripId, CancellationToken ct);
}
