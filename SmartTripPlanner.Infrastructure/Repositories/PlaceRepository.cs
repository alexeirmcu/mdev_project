using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Constants;
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
        // Database-agnostic case-insensitive search using ToLower() on both sides
        // This works across all SQL providers (PostgreSQL, SQL Server, SQLite, etc.)
        var lowerQuery = query.ToLowerInvariant();

        return await _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .Include(p => p.City)
            .Where(p => p.City.CityCode == cityCode
                && (p.Name.ToLower().Contains(lowerQuery)
                    || p.Attributes.Any(a => a.Value.ToLower().Contains(lowerQuery))))
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

    public async Task<IEnumerable<Place>> GetManyByIdsAsync(IEnumerable<long> placeIds, CancellationToken ct)
    {
        return await _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .Where(p => placeIds.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task<List<Place>> GetManyByCityIdAsync(long cityId, IEnumerable<string>? interests = null, CancellationToken ct = default)
    {
        var query = _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .Where(p => p.CityId == cityId)
            .AsQueryable();

        if (interests != null && interests.Any())
        {
            query = query.Where(p => p.Attributes.Any(a => interests.Contains(a.Value)));
        }

        return await query
            .Take(TripPlanningConstants.MaxCandidatesPerCity)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetDistinctInterestsByCityIdAsync(long cityId, CancellationToken ct = default)
    {
        return await _context.Places
            .Where(p => p.CityId == cityId)
            .SelectMany(p => p.Attributes)
            .Select(a => a.Value)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync(ct);
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
