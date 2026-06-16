using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Mapping;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;

internal sealed class FoursquarePlaceService : IPlaceExternalService
{
    private readonly IFoursquareApiClient _apiClient;

    public FoursquarePlaceService(IFoursquareApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task<List<Place>> SearchPlacesAsync(string query, string cityCode, long cityId, int maxResults = 20)
    {
        try
        {
            var apiResults = await _apiClient.SearchPlacesAsync(query, cityCode, maxResults);
            return apiResults.Select(p => MapToPlace(p, cityId)).ToList();
        }
        catch (HttpRequestException)
        {
            return new List<Place>();
        }
    }

    private static Place MapToPlace(FoursquarePlace apiPlace, long cityId)
    {
        var location = new PlaceLocation(apiPlace.Latitude, apiPlace.Longitude);

        var (duration, isIndoor, isFamilyFriendly) =
            FoursquareCategoryHeuristics.Map(apiPlace.Categories);

        return new Place(
            apiPlace.FsqPlaceId,
            apiPlace.Name,
            cityId, location, duration, isIndoor, isFamilyFriendly,
            Domain.Enums.Provider.Foursquare);
    }
}
