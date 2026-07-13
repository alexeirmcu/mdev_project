using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Tests.Helpers;

namespace SmartTripPlanner.Tests.Integration;

/// <summary>
/// Integration tests for Trip ownership via JWT Bearer.
/// Uses WebApplicationFactory&lt;Program&gt; with EF InMemory (avoids Testcontainers/Postgres).
/// </summary>
[TestClass]
public sealed class TripsControllerAuthTests
{
    private static WebApplicationFactory<Program> _factory = null!;
    private static HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string User42Token = "Bearer " + TestJwtTokenFactory.CreateToken("user-42");
    private static readonly string User99Token = "Bearer " + TestJwtTokenFactory.CreateToken("user-99");
    private static readonly string ExpiredToken = "Bearer " + TestJwtTokenFactory.CreateToken("user-42", expiryHours: -1);

    private static TripGenerationRequest CreateSampleRequest()
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        return new TripGenerationRequest(
        CityCode: "madrid-es",
        StartDate: startDate,
        EndDate: startDate.AddDays(2),
        BaseHotel: new LocationModel("Hotel Central", 40.4168, -3.7038),
        MustSees: null,
        Travelers: new TravelersInput(2, 0, 0),
        Preferences: new TripPreferencesInput(false, 30, true, Interests: new List<string> { "culture", "food" }),
        DefaultStartHour: "09:00");
    }

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");

                builder.ConfigureTestServices(services =>
                {
                    // Remove ALL real PlannerDbContext-related registrations
                    var descriptorsToRemove = services
                        .Where(s => s.ServiceType == typeof(PlannerDbContext)
                                 || s.ServiceType == typeof(DbContextOptions<PlannerDbContext>)
                                 || s.ServiceType == typeof(DbContextOptions))
                        .ToList();
                    foreach (var descriptor in descriptorsToRemove)
                        services.Remove(descriptor);

                    // Add InMemory database for tests
                    services.AddDbContext<PlannerDbContext>(options =>
                        options.UseInMemoryDatabase("TripsControllerAuthTests_DB"),
                        ServiceLifetime.Scoped);
                });

                builder.UseSetting("Jwt:Secret", TestJwtTokenFactory.GetSecret());
                builder.UseSetting("Jwt:Issuer", "smart-trip-planner");
                builder.UseSetting("Jwt:Audience", "smart-trip-planner-api");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test");
                builder.UseSetting("FoursquareApi:ApiKey", "test-key");
                builder.UseSetting("LlmApi:ApiKey", "test-key");
            });

        _client = _factory.CreateClient();

        // Seed test data: a city so POST /api/trips succeeds
        SeedTestData();
    }

    private static void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();

        if (!db.Set<City>().Any(c => c.CityCode == "madrid-es"))
        {
            // Use reflection to set _Id since City inherits from Entity with private _Id
            var city = new City("madrid-es", "Madrid", true);
            var idField = typeof(Entity).GetField("_Id",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            idField!.SetValue(city, 1L);
            db.Add(city);
            db.SaveChanges();
        }
    }

    private static void SetEntityId<T>(T entity, long id) where T : class
    {
        var entityType = typeof(Entity);
        var field = entityType.GetField("_Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(entity, id);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────
    // S4 — Request without JWT is rejected (401)
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task PostTrips_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/trips", CreateSampleRequest(), JsonOptions);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTrips_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync($"/api/trips/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Malformed/expired token is rejected (401)
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task PostTrips_WithMalformedToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/trips")
        {
            Content = JsonContent.Create(CreateSampleRequest(), options: JsonOptions),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt-token") }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PostTrips_WithExpiredToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/trips")
        {
            Content = JsonContent.Create(CreateSampleRequest(), options: JsonOptions),
            Headers = { Authorization = AuthenticationHeaderValue.Parse(ExpiredToken) }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────
    // S1 — Create trip with valid JWT sets OwnerUserId from `sub` (201)
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task PostTrips_WithValidToken_Returns201()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/trips")
        {
            Content = JsonContent.Create(CreateSampleRequest(), options: JsonOptions),
            Headers = { Authorization = AuthenticationHeaderValue.Parse(User42Token) }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TripPlanResponse>(JsonOptions);
        Assert.IsNotNull(body);
        Assert.AreNotEqual(Guid.Empty, body!.TripId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // S2 — Get trip with matching owner returns 200
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTrips_WithMatchingOwner_Returns200()
    {
        // First create a trip as user-42
        var tripId = await CreateTripAsync(User42Token);

        // Then GET it as user-42
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/trips/{tripId}")
        {
            Headers = { Authorization = AuthenticationHeaderValue.Parse(User42Token) }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TripPlanResponse>(JsonOptions);
        Assert.IsNotNull(body);
        Assert.AreEqual(tripId, body!.TripId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // S3 — Get trip with non-matching owner returns 403
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTrips_WithNonMatchingOwner_Returns403()
    {
        // First create a trip as user-42
        var tripId = await CreateTripAsync(User42Token);

        // Then GET it as user-99 → 403 Forbidden
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/trips/{tripId}")
        {
            Headers = { Authorization = AuthenticationHeaderValue.Parse(User99Token) }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────
    // S7 — Delete trip with matching owner returns 204
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteTrips_WithMatchingOwner_Returns204()
    {
        var tripId = await CreateTripAsync(User42Token);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/trips/{tripId}")
        {
            Headers = { Authorization = AuthenticationHeaderValue.Parse(User42Token) }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────
    // S8 — Get non-existent trip returns 404
    // ─────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTrips_NonExistent_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/trips/{Guid.NewGuid()}")
        {
            Headers = { Authorization = AuthenticationHeaderValue.Parse(User42Token) }
        };

        var response = await _client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateTripAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/trips")
        {
            Content = JsonContent.Create(CreateSampleRequest(), options: JsonOptions),
            Headers = { Authorization = AuthenticationHeaderValue.Parse(token) }
        };

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TripPlanResponse>(JsonOptions);
        return body!.TripId;
    }
}
