using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Infrastructure.Repositories;

internal sealed class TripRepository : ITripRepository
{
    private readonly PlannerDbContext _dbContext;

    public TripRepository(PlannerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => _dbContext;

    public async Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken ct)
    {
        return await _dbContext.Trips
            .Include(t => t.City)
            .Include(t => t.Days)
            .FirstOrDefaultAsync(t => t.TripId == tripId, ct);
    }

    public async Task<Trip?> GetByTripCodeAsync(string tripCode, CancellationToken ct)
    {
        return await _dbContext.Trips
            .Include(t => t.City)
            .Include(t => t.Days)
            .FirstOrDefaultAsync(t => t.TripCode == tripCode, ct);
    }

    public async Task<bool> ExistsByTripCodeAsync(string tripCode, CancellationToken ct)
    {
        return await _dbContext.Trips.AnyAsync(t => t.TripCode == tripCode, ct);
    }

    public async Task<IEnumerable<Trip>> ListAsync(long? cityId, DateOnly? startDate, DateOnly? endDate, CancellationToken ct)
    {
        var query = _dbContext.Trips
            .Include(t => t.City)
            .Include(t => t.Days)
            .AsQueryable();

        if (cityId.HasValue)
            query = query.Where(t => t.CityId == cityId.Value);

        if (startDate.HasValue)
            query = query.Where(t => t.StartDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.EndDate <= endDate.Value);

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(Trip trip, CancellationToken ct)
    {
        await _dbContext.Trips.AddAsync(trip, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Trip trip, CancellationToken ct)
    {
        _dbContext.Trips.Update(trip);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid tripId, CancellationToken ct)
    {
        var trip = await GetByIdAsync(tripId, ct);
        if (trip is not null)
        {
            _dbContext.Trips.Remove(trip);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
