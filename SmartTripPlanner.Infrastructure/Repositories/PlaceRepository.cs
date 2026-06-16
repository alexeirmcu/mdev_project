using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Infrastructure.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly PlannerDbContext _context;

    public PlaceRepository(PlannerDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<List<Place>> SearchAsync(string query, string cityCode, int maxResults = 20)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.City)
            .Where(p => p.Name.Contains(query) && p.City.CityCode == cityCode)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.ProviderReferenceId == providerReferenceId);
    }

    public async Task AddRangeAsync(IEnumerable<Place> places)
    {
        await _context.Places.AddRangeAsync(places);
    }
}
