using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure;

namespace SmartTripPlanner.Infrastructure.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly PlannerDbContext _context;

    public PlaceRepository(PlannerDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<List<Place>> SearchAsync(string query, string cityId, int maxResults = 20)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .Where(p => p.Name.Contains(query) && p.CityId == cityId)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<Place?> GetByPlaceIdAsync(string placeId)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.PlaceId == placeId);
    }
}
