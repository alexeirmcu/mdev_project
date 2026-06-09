using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartTripPlanner.Infrastructure.Configuration;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Foursquare;

[TestClass]
public sealed class FoursquareApiClientTests
{
    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static FoursquareApiOptions CreateOptions() => new()
    {
        ApiKey = "test-api-key",
        BaseUrl = "https://api.foursquare.com/v3/"
    };

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.foursquare.com/v3/") };
    }

    [TestMethod]
    public async Task SearchPlacesAsync_SuccessfulResponse_ReturnsMappedPlaces()
    {
        var places = new List<FoursquarePlace>
        {
            new()
            {
                FsqId = "fsq1",
                Name = "Museo del Prado",
                Geocodes = new FoursquareGeocodes { Main = new FoursquareLatLng { Latitude = 40.4168, Longitude = -3.7038 } },
                Categories = new List<FoursquareCategory> { new() { Id = "10000", Name = "Museum" } }
            },
            new()
            {
                FsqId = "fsq2",
                Name = "Reina Sofia",
                Geocodes = new FoursquareGeocodes { Main = new FoursquareLatLng { Latitude = 40.4089, Longitude = -3.6944 } },
                Categories = new List<FoursquareCategory> { new() { Id = "10000", Name = "Museum" } }
            }
        };
        var json = JsonSerializer.Serialize(new { results = places }, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.SearchPlacesAsync("museum", "madrid");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("fsq1", result[0].FsqId);
        Assert.AreEqual("Museo del Prado", result[0].Name);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_EmptyResponse_ReturnsEmptyList()
    {
        var json = JsonSerializer.Serialize(new { results = Array.Empty<FoursquarePlace>() }, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.SearchPlacesAsync("museum", "madrid");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_HttpError_ReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.InternalServerError);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.SearchPlacesAsync("museum", "madrid");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetPlaceByIdAsync_ValidId_ReturnsPlace()
    {
        var place = new FoursquarePlace
        {
            FsqId = "fsq123",
            Name = "Museo del Prado",
            Geocodes = new FoursquareGeocodes { Main = new FoursquareLatLng { Latitude = 40.4168, Longitude = -3.7038 } },
            Categories = new List<FoursquareCategory> { new() { Id = "10000", Name = "Museum" } }
        };
        var json = JsonSerializer.Serialize(place, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.GetPlaceByIdAsync("fsq123");

        Assert.IsNotNull(result);
        Assert.AreEqual("fsq123", result.FsqId);
        Assert.AreEqual("Museo del Prado", result.Name);
    }

    [TestMethod]
    public async Task GetPlaceByIdAsync_NonExistentId_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.NotFound);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.GetPlaceByIdAsync("nonexistent");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_SetsAuthHeader()
    {
        var json = JsonSerializer.Serialize(new { results = Array.Empty<FoursquarePlace>() }, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-api-key");
        var client = new FoursquareApiClient(httpClient);

        await client.SearchPlacesAsync("museum", "madrid");

        var authHeader = handler.LastRequest?.Headers.Authorization;
        Assert.IsNotNull(authHeader);
        Assert.AreEqual("Bearer", authHeader.Scheme);
        Assert.AreEqual("test-api-key", authHeader.Parameter);
    }

    private sealed class MockHttpMessageHandler : DelegatingHandler
    {
        private readonly string _responseContent;
        private readonly HttpStatusCode _statusCode;

        public HttpRequestMessage? LastRequest { get; private set; }

        public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
