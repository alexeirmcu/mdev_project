using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Configuration;
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
        BaseUrl = "https://places-api.foursquare.com/"
    };

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler) { BaseAddress = new Uri("https://places-api.foursquare.com/") };
    }

    [TestMethod]
    public async Task SearchPlacesAsync_SuccessfulResponse_ReturnsMappedPlaces()
    {
        var places = new List<FoursquarePlace>
        {
            new()
            {
                FsqPlaceId = "fsq1",
                Name = "Museo del Prado",
                Latitude = 40.4168,
                Longitude = -3.7038,
                Categories = new List<FoursquareCategory> { new() { FsqCategoryId = "10000", Name = "Museum" } }
            },
            new()
            {
                FsqPlaceId = "fsq2",
                Name = "Reina Sofia",
                Latitude = 40.4089,
                Longitude = -3.6944,
                Categories = new List<FoursquareCategory> { new() { FsqCategoryId = "10000", Name = "Museum" } }
            }
        };
        var json = JsonSerializer.Serialize(new { results = places }, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.SearchPlacesAsync("museum", "madrid");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("fsq1", result[0].FsqPlaceId);
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
            FsqPlaceId = "fsq123",
            Name = "Museo del Prado",
            Latitude = 40.4168,
            Longitude = -3.7038,
            Categories = new List<FoursquareCategory> { new() { FsqCategoryId = "10000", Name = "Museum" } }
        };
        var json = JsonSerializer.Serialize(place, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var options = Options.Create(CreateOptions());
        var client = new FoursquareApiClient(httpClient);

        var result = await client.GetPlaceByIdAsync("fsq123");

        Assert.IsNotNull(result);
        Assert.AreEqual("fsq123", result.FsqPlaceId);
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

    [TestMethod]
    public async Task SearchPlacesAsync_WithCategoryIds_AddsCategoryParamToUrl()
    {
        var json = JsonSerializer.Serialize(new { results = Array.Empty<FoursquarePlace>() }, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-api-key");
        var client = new FoursquareApiClient(httpClient);

        await client.SearchPlacesAsync("museum", "madrid", 20, new List<string> { "10000", "13002" });

        Assert.IsNotNull(handler.LastRequest);
        var url = handler.LastRequest.RequestUri.ToString();
        StringAssert.Contains(url, "fsq_category_ids=10000,13002");
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithoutCategoryIds_NoCategoryParam()
    {
        var json = JsonSerializer.Serialize(new { results = Array.Empty<FoursquarePlace>() }, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-api-key");
        var client = new FoursquareApiClient(httpClient);

        // Null fsqCategoryIds
        await client.SearchPlacesAsync("museum", "madrid", 20, null);

        Assert.IsNotNull(handler.LastRequest);
        var url = handler.LastRequest.RequestUri.ToString();
        Assert.IsFalse(url.Contains("categories"), "URL should not contain 'categories' parameter when no category IDs are provided");
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
