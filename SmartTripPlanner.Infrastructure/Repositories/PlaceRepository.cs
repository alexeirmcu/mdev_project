using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
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

    private async Task<ICollection<PlaceAttribute>> ResolveAttributesAsync(IEnumerable<PlaceAttribute> attributes)
    {
        var resolved = new List<PlaceAttribute>();
        var seen = new Dictionary<string, PlaceAttribute>(StringComparer.OrdinalIgnoreCase);

        foreach (var attr in attributes)
        {
            var normalizedProvider = attr.Provider.ToLowerInvariant();
            var normalizedKey = attr.Key.ToLowerInvariant();
            var normalizedValue = attr.Value.ToLowerInvariant();
            var lookupKey = $"{normalizedProvider}|{normalizedKey}|{normalizedValue}";

            // Deduplicate within this resolution batch
            if (seen.TryGetValue(lookupKey, out var cached))
            {
                resolved.Add(cached);
                continue;
            }

            // Query database for already-saved entities
            var existing = await _context.PlaceAttributes
                .FirstOrDefaultAsync(a =>
                    a.Provider.ToLower() == normalizedProvider &&
                    a.Key.ToLower() == normalizedKey &&
                    a.Value.ToLower() == normalizedValue);

            // Fallback: check locally tracked entities (Added but not yet persisted)
            if (existing == null)
            {
                existing = _context.PlaceAttributes.Local
                    .FirstOrDefault(a =>
                        a.Provider.ToLowerInvariant() == normalizedProvider &&
                        a.Key.ToLowerInvariant() == normalizedKey &&
                        a.Value.ToLowerInvariant() == normalizedValue);
            }

            if (existing != null)
            {
                // Preserve ProviderId from DB if incoming has none, update if incoming has one
                if (!string.IsNullOrEmpty(attr.ProviderId))
                    existing.UpdateProviderId(attr.ProviderId);

                resolved.Add(existing);
                seen[lookupKey] = existing;
            }
            else
            {
                var created = new PlaceAttribute(attr.Provider, attr.Key, attr.Value, attr.ProviderId);
                _context.PlaceAttributes.Add(created);
                resolved.Add(created);
                seen[lookupKey] = created;
            }
        }

        return resolved;
    }

    public async Task<List<Place>> SearchAsync(string? query, string cityCode, int maxResults = 20, PlaceSearchFilter? filter = null)
    {
        // Build base query with city filter
        var queryable = _context.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .Include(p => p.City)
            .Where(p => p.City.CityCode == cityCode)
            .AsQueryable();

        // Apply text filter only when query is non-null
        if (!string.IsNullOrEmpty(query))
        {
            var lowerQuery = query.ToLowerInvariant();
            queryable = queryable.Where(p =>
                p.Name.ToLower().Contains(lowerQuery)
                || p.Attributes.Any(a => a.Value.ToLower().Contains(lowerQuery)));
        }

        // Apply optional filters server-side so the database does the work
        if (filter is not null)
        {
            if (filter.IsIndoor.HasValue)
                queryable = queryable.Where(p => p.IsIndoor == filter.IsIndoor.Value);

            if (filter.IsFamilyFriendly.HasValue)
                queryable = queryable.Where(p => p.IsFamilyFriendly == filter.IsFamilyFriendly.Value);

            if (filter.MaxDurationMinutes.HasValue)
                queryable = queryable.Where(p => p.TypicalDurationMinutes <= filter.MaxDurationMinutes.Value);

            if (!string.IsNullOrEmpty(filter.Category))
            {
                var lowerCategory = filter.Category.ToLowerInvariant();
                queryable = queryable.Where(p => p.Attributes.Any(a =>
                    a.Key.ToLower() == "category" &&
                    a.Value.ToLower().Contains(lowerCategory)));
            }
        }

        return await queryable
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<string?> GetProviderIdForCategoryAsync(string categoryName, CancellationToken ct = default)
    {
        return await _context.PlaceAttributes
            .Where(a => a.Provider.ToLower() == "foursquare"
                && a.Key.ToLower() == "category"
                && a.Value.ToLower() == categoryName.ToLowerInvariant())
            .Select(a => a.ProviderId)
            .FirstOrDefaultAsync(ct);
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
            query = query.Where(BuildInterestPredicate(interests));
        }

        return await query
            .Take(TripPlanningConstants.MaxCandidatesPerCity)
            .ToListAsync(ct);
    }

    private static Expression<Func<Place, bool>> BuildInterestPredicate(IEnumerable<string> interests)
    {
        var lowerInterests = interests.Select(i => i.ToLowerInvariant()).ToList();

        var placeParam = Expression.Parameter(typeof(Place), "p");
        var attributesProp = Expression.Property(placeParam, nameof(Place.Attributes));

        var attrParam = Expression.Parameter(typeof(PlaceAttribute), "a");
        var keyProp = Expression.Property(attrParam, nameof(PlaceAttribute.Key));
        var valueProp = Expression.Property(attrParam, nameof(PlaceAttribute.Value));

        var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;

        // a.Key.ToLower() == TripPlanningConstants.InterestAttributeKey
        var keyMatch = Expression.Equal(
            Expression.Call(keyProp, toLower),
            Expression.Constant(TripPlanningConstants.InterestAttributeKey));

        // a.Value.ToLower().Contains(i1) || a.Value.ToLower().Contains(i2) || ...
        Expression? valueMatch = null;
        foreach (var interest in lowerInterests)
        {
            var match = Expression.Call(
                Expression.Call(valueProp, toLower),
                contains,
                Expression.Constant(interest));
            valueMatch = valueMatch == null ? match : Expression.OrElse(valueMatch, match);
        }

        var attrLambda = Expression.Lambda<Func<PlaceAttribute, bool>>(
            Expression.AndAlso(keyMatch, valueMatch!),
            attrParam);

        // p.Attributes.Any(a => ...)
        var anyMethod = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Any) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(PlaceAttribute));

        var anyCall = Expression.Call(anyMethod, attributesProp, attrLambda);

        return Expression.Lambda<Func<Place, bool>>(anyCall, placeParam);
    }

    public async Task<List<string>> GetDistinctInterestsByCityIdAsync(long cityId, CancellationToken ct = default)
    {
        return await _context.Places
            .Where(p => p.CityId == cityId)
            .SelectMany(p => p.Attributes)
            .Where(a => a.Key.ToLower() == TripPlanningConstants.InterestAttributeKey)
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

            // Resolve attributes: find-or-create by normalized (Provider, Key, Value)
            var resolvedAttributes = await ResolveAttributesAsync(place.Attributes);

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
                        resolvedAttributes);
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
                // Replace incoming attributes with resolved (tracked) entities
                place.Attributes.Clear();
                foreach (var attr in resolvedAttributes)
                    place.Attributes.Add(attr);

                await _context.Places.AddAsync(place);
            }
        }
    }
}
