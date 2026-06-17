using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Infrastructure.Repositories;

internal sealed class CityRepository : ICityRepository
{
    private readonly PlannerDbContext _dbContext;

    public CityRepository(PlannerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<City?> GetByCodeAsync(string cityCode, CancellationToken ct)
    {
        return await _dbContext.Cities
            .FirstOrDefaultAsync(c => c.CityCode == cityCode && c.IsAllowed, ct);
    }

    public async Task<City?> GetByIdAsync(long id, CancellationToken ct)
    {
        return await _dbContext.Cities
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IEnumerable<City>> ListAllowedAsync(CancellationToken ct)
    {
        return await _dbContext.Cities
            .Where(c => c.IsAllowed)
            .ToListAsync(ct);
    }

    public IUnitOfWork UnitOfWork => _dbContext;
}
