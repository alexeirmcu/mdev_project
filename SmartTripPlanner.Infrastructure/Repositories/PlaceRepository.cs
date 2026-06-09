using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Mapping;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Infrastructure.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly PlannerDbContext _context;
    private readonly IFoursquareApiClient? _foursquareApiClient;

    public PlaceRepository(PlannerDbContext context, IFoursquareApiClient? foursquareApiClient = null)
    {
        _context = context;
        _foursquareApiClient = foursquareApiClient;
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

        if (_foursquareApiClient is null)
            return new List<Place>();

        try
        {
            var apiResults = await _foursquareApiClient.SearchPlacesAsync(query, cityId, maxResults);
            return apiResults.Select(p => MapToPlace(p, cityId)).ToList();
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

    private Place MapToPlace(FoursquarePlace apiPlace, string cityId)
    {
        var location = apiPlace.Geocodes?.Main is not null
            ? new PlaceLocation(apiPlace.Geocodes.Main.Latitude, apiPlace.Geocodes.Main.Longitude)
            : new PlaceLocation(0, 0);

        var (duration, isIndoor, isFamilyFriendly) =
            FoursquareCategoryHeuristics.Map(apiPlace.Categories);

        return new Place(
            apiPlace.FsqId,
            apiPlace.Name,
            cityId, location, duration, isIndoor, isFamilyFriendly);
    }
}
