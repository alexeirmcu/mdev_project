using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Infrastructure.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly PlannerDbContext _context;
    private readonly ILogger<PlaceRepository> _logger;

    public PlaceRepository(PlannerDbContext context, ILogger<PlaceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<List<Place>> SearchAsync(string query, string cityCode, int maxResults = 20)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .Include(p => p.City)
            .Where(p => p.City.CityCode == cityCode
                && (EF.Functions.Like(p.Name, $"%{query}%") || p.Attributes.Any(a => EF.Functions.Like(a.Value, $"%{query}%"))))
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.ProviderReferenceId == providerReferenceId);
    }

    public async Task AddRangeAsync(IEnumerable<Place> places)
    {
        await _context.Places.AddRangeAsync(places);
    }

    public async Task UpsertRangeAsync(IEnumerable<Place> places)
    {
        foreach (var place in places)
        {
            var existing = await GetByProviderReferenceIdAsync(place.ProviderReferenceId);
            if (existing != null)
            {
                if (existing.IsAutoUpdateEnabled)
                {
                    existing.UpdateFromExternalProvider(
                        place.Name,
                        place.Location,
                        place.TypicalDurationMinutes,
                        place.IsIndoor,
                        place.IsFamilyFriendly,
                        place.Attributes);
                }
                else
                {
                    _logger.LogWarning(
                        "Place {ProviderReferenceId} already exists and auto-update is disabled. Skipping update.",
                        place.ProviderReferenceId);
                }
            }
            else
            {
                await _context.Places.AddAsync(place);
            }
        }
    }
}
