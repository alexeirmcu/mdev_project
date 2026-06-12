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

    public async Task<City?> GetByIdAsync(string cityId)
    {
        return await _dbContext.Cities
            .FirstOrDefaultAsync(c => c.CityCode == cityId && c.IsAllowed);
    }

    public IUnitOfWork UnitOfWork => _dbContext;
}