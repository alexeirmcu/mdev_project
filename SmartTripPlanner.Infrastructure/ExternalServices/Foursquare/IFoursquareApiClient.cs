using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;

internal interface IFoursquareApiClient
{
    Task<List<FoursquarePlace>> SearchPlacesAsync(string query, string near, int limit = 20, List<string>? fsqCategoryIds = null);
    Task<FoursquarePlace?> GetPlaceByIdAsync(string fsqId, bool includeTips = false, CancellationToken ct = default);
}
