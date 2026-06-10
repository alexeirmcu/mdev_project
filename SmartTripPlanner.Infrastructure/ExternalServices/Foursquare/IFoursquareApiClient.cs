using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;

internal interface IFoursquareApiClient
{
    Task<List<FoursquarePlace>> SearchPlacesAsync(string query, string near, int limit = 20);
    Task<FoursquarePlace?> GetPlaceByIdAsync(string fsqId);
}
