using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
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

    public async Task<List<Place>> SearchPlacesAsync(string query, string cityCode, long cityId, int maxResults = 20,
        PlaceSearchFilter? filter = null, List<string>? fsqCategoryIds = null)
    {
        try
        {
            var apiResults = await _apiClient.SearchPlacesAsync(query, cityCode, maxResults, fsqCategoryIds);
            var places = apiResults.Select(p => MapToPlace(p, cityId)).ToList();

            if (filter is not null)
                places = ApplyClientFilters(places, filter);

            return places;
        }
        catch (HttpRequestException)
        {
            return new List<Place>();
        }
    }

    private static List<Place> ApplyClientFilters(List<Place> places, PlaceSearchFilter filter)
    {
        var filtered = new List<Place>(places.Count);

        foreach (var place in places)
        {
            bool include = true;

            if (include && filter.IsIndoor.HasValue)
                include = place.IsIndoor == filter.IsIndoor.Value;

            if (include && filter.IsFamilyFriendly.HasValue)
                include = place.IsFamilyFriendly == filter.IsFamilyFriendly.Value;

            if (include && filter.MaxDurationMinutes.HasValue)
                include = place.TypicalDurationMinutes <= filter.MaxDurationMinutes.Value;

            if (include && !string.IsNullOrEmpty(filter.Category))
            {
                var lowerCategory = filter.Category.ToLowerInvariant();
                include = place.Attributes.Any(a =>
                    a.Key.Equals("category", StringComparison.OrdinalIgnoreCase) &&
                    a.Value.Contains(lowerCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (include)
                filtered.Add(place);
        }

        return filtered;
    }

    private static Place MapToPlace(FoursquarePlace apiPlace, long cityId)
    {
        var location = new PlaceLocation(apiPlace.Latitude, apiPlace.Longitude);

        var (duration, isIndoor, isFamilyFriendly) =
            FoursquareCategoryHeuristics.Map(apiPlace.Categories);

        var place = new Place(
            apiPlace.FsqPlaceId,
            apiPlace.Name,
            cityId, location, duration, isIndoor, isFamilyFriendly,
            Domain.Enums.Provider.Foursquare);

        foreach (var category in apiPlace.Categories)
        {
            place.AddAttribute(new PlaceAttribute("foursquare", "category", category.Name, category.FsqCategoryId));
        }

        // Inject default opening hours for solver compatibility
        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 540, 1080)); // 09:00-18:00

        return place;
    }
}
