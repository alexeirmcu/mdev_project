using System.Net;
using System.Text;
using System.Text.Json;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Models;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Weather;

[TestClass]
public sealed class OpenMeteoApiClientTests
{
    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com/") };
    }

    [TestMethod]
    public async Task GetForecastAsync_SuccessfulResponse_ReturnsMappedForecasts()
    {
        var response = new OpenMeteoForecastResponse
        {
            Daily = new OpenMeteoDailyData
            {
                Time = new List<string> { "2026-06-25", "2026-06-26" },
                WeatherCode = new List<int> { 0, 95 },
                TemperatureMax = new List<double> { 28.0, 22.0 },
                TemperatureMin = new List<double> { 15.0, 12.0 }
            }
        };
        var json = JsonSerializer.Serialize(response, JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var client = new OpenMeteoApiClient(httpClient);

        var result = await client.GetForecastAsync(40.4168, -3.7038,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 26));

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(new DateOnly(2026, 6, 25), result[0].Date);
        Assert.AreEqual(0, result[0].WeatherCode);
        Assert.AreEqual(28.0, result[0].TempMax);
        Assert.AreEqual(15.0, result[0].TempMin);
        Assert.AreEqual(new DateOnly(2026, 6, 26), result[1].Date);
        Assert.AreEqual(95, result[1].WeatherCode);
        Assert.AreEqual(22.0, result[1].TempMax);
        Assert.AreEqual(12.0, result[1].TempMin);
    }

    [TestMethod]
    public async Task GetForecastAsync_Http500_ReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.InternalServerError);
        using var httpClient = CreateClient(handler);
        var client = new OpenMeteoApiClient(httpClient);

        var result = await client.GetForecastAsync(40.4168, -3.7038,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 26));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetForecastAsync_EmptyResponse_ReturnsEmptyList()
    {
        var json = JsonSerializer.Serialize(new OpenMeteoForecastResponse(), JsonOptions);

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        using var httpClient = CreateClient(handler);
        var client = new OpenMeteoApiClient(httpClient);

        var result = await client.GetForecastAsync(40.4168, -3.7038,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 26));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetForecastAsync_HttpRequestException_ReturnsEmptyList()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Network error"));
        using var httpClient = CreateClient(handler);
        var client = new OpenMeteoApiClient(httpClient);

        var result = await client.GetForecastAsync(40.4168, -3.7038,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 26));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetForecastAsync_TaskCanceledException_ReturnsEmptyList()
    {
        var handler = new ThrowingHttpMessageHandler(new TaskCanceledException("Timeout"));
        using var httpClient = CreateClient(handler);
        var client = new OpenMeteoApiClient(httpClient);

        var result = await client.GetForecastAsync(40.4168, -3.7038,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 26));

        Assert.AreEqual(0, result.Count);
    }

    private sealed class ThrowingHttpMessageHandler : DelegatingHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }

    private sealed class MockHttpMessageHandler : DelegatingHandler
    {
        private readonly string _responseContent;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
