using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;

internal class FoursquareApiClient : IFoursquareApiClient
{
    private const string BasePath = "/places";

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public FoursquareApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FoursquarePlace>> SearchPlacesAsync(string query, string near, int limit = 20)
    {
        try
        {
            var url = $"{BasePath}/search?query={Uri.EscapeDataString(query)}&near={Uri.EscapeDataString(near)}&limit={limit}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<FoursquarePlace>();

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<FoursquareSearchResponse>(json, JsonOptions);
            return wrapper?.Results ?? new List<FoursquarePlace>();
        }
        catch (HttpRequestException)
        {
            return new List<FoursquarePlace>();
        }
    }

    public async Task<FoursquarePlace?> GetPlaceByIdAsync(string fsqId)
    {
        try
        {
            var url = $"{BasePath}/{Uri.EscapeDataString(fsqId)}?fields=fsq_id,name,geocodes,hours,categories";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FoursquarePlace>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private sealed class FoursquareSearchResponse
    {
        [JsonPropertyName("results")]
        public List<FoursquarePlace> Results { get; set; } = new();
    }
}
