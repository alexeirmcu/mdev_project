using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;
 
namespace SmartTripPlanner.Infrastructure.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly PlannerDbContext _context;
    private readonly IPlaceExternalService? _externalService;

    public PlaceRepository(PlannerDbContext context, IPlaceExternalService? externalService = null)
    {
        _context = context;
        _externalService = externalService;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<List<Place>> SearchAsync(string query, string cityId, int maxResults = 20)
    {
        var localResults = await _context.Places
            .Include(p => p.OpeningHours)
            .Where(p => p.Name.Contains(query) && p.CityId == cityId)
            .Take(maxResults)
            .ToListAsync();

        if (localResults.Count > 0)
            return localResults;

        if (_externalService is null)
            return new List<Place>();

        try
        {
            return await _externalService.SearchPlacesAsync(query, cityId, maxResults);
        }
        catch (HttpRequestException)
        {
            return new List<Place>();
        }
    }

    public async Task<Place?> GetByPlaceIdAsync(string placeId)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.PlaceId == placeId);
    }
}
