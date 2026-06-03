using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Repository;

public interface ITripRepository : IRepository<Trip>
{
    Task<Trip?> GetByIdAsync(Guid tripId);
    Task<IEnumerable<Trip>> ListAsync(string? cityId, DateOnly? startDate, DateOnly? endDate);
    Task AddAsync(Trip trip, CancellationToken ct);
    Task UpdateAsync(Trip trip, CancellationToken ct);
}
