using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;
using SmartTripPlanner.Infrastructure.Repositories;

namespace SmartTripPlanner.Tests.Infrastructure.Repositories;

[TestClass]
public sealed class PlaceRepositoryCascadeTests
{
    private static PlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlannerDbContext(options);
    }

    private sealed class MockApiClient : IFoursquareApiClient
    {
        private readonly List<FoursquarePlace>? _results;
        private readonly HttpStatusCode _statusCode;

        public MockApiClient(List<FoursquarePlace> results)
        {
            _results = results;
            _statusCode = HttpStatusCode.OK;
        }

        public MockApiClient(HttpStatusCode statusCode)
        {
            _results = null;
            _statusCode = statusCode;
        }

        public bool WasCalled { get; private set; }

        public Task<List<FoursquarePlace>> SearchPlacesAsync(string query, string near, int limit = 20)
        {
            WasCalled = true;
            if (_statusCode != HttpStatusCode.OK || _results is null)
                return Task.FromResult(new List<FoursquarePlace>());
            return Task.FromResult(_results);
        }

        public Task<FoursquarePlace?> GetPlaceByIdAsync(string fsqId)
        {
            WasCalled = true;
            return Task.FromResult<FoursquarePlace?>(null);
        }
    }

    [TestMethod]
    public async Task SearchAsync_LocalResultsExist_ReturnsLocal_NoApiCall()
    {
        using var db = CreateDbContext();
        db.Places.Add(new Place("f1", "Museo del Prado", "madrid-es", new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Museo Reina Sofia", "madrid-es", new PlaceLocation(40.4089, -3.6944)));
        await db.SaveChangesAsync();

        var mockApi = new MockApiClient(new List<FoursquarePlace>());
        var repo = new PlaceRepository(db, mockApi);

        var results = await repo.SearchAsync("Museo", "madrid-es");

        Assert.AreEqual(2, results.Count);
        Assert.IsFalse(mockApi.WasCalled, "API should NOT be called when local results exist");
    }

    [TestMethod]
    public async Task SearchAsync_NoLocalResults_CallsApi_ReturnsMapped()
    {
        using var db = CreateDbContext();
        var apiPlaces = new List<FoursquarePlace>
        {
            new()
            {
                FsqId = "fsq1",
                Name = "Museo del Prado",
                Geocodes = new FoursquareGeocodes { Main = new FoursquareLatLng { Latitude = 40.4168, Longitude = -3.7038 } },
                Categories = new List<FoursquareCategory> { new() { Id = "10000", Name = "Museum" } }
            }
        };
        var mockApi = new MockApiClient(apiPlaces);
        var repo = new PlaceRepository(db, mockApi);

        var results = await repo.SearchAsync("Museo", "madrid-es");

        Assert.IsTrue(mockApi.WasCalled, "API should be called when no local results");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("fsq1", results[0].PlaceId);
        Assert.AreEqual("Museo del Prado", results[0].Name);
        Assert.AreEqual("madrid-es", results[0].CityId);
    }

    [TestMethod]
    public async Task SearchAsync_NoLocalResults_ApiError_ReturnsEmpty()
    {
        using var db = CreateDbContext();
        var mockApi = new MockApiClient(HttpStatusCode.InternalServerError);
        var repo = new PlaceRepository(db, mockApi);

        var results = await repo.SearchAsync("Museo", "madrid-es");

        Assert.IsTrue(mockApi.WasCalled, "API should be called");
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_SavedPlaces_NotPersistedFromApi()
    {
        using var db = CreateDbContext();
        var apiPlaces = new List<FoursquarePlace>
        {
            new()
            {
                FsqId = "fsq_api_1",
                Name = "Prado from API",
                Geocodes = new FoursquareGeocodes { Main = new FoursquareLatLng { Latitude = 40.4168, Longitude = -3.7038 } },
                Categories = new List<FoursquareCategory> { new() { Id = "10000", Name = "Museum" } }
            }
        };
        var mockApi = new MockApiClient(apiPlaces);
        var repo = new PlaceRepository(db, mockApi);

        var results = await repo.SearchAsync("Prado", "madrid-es");

        Assert.AreEqual(1, results.Count);

        var savedCount = await db.Places.CountAsync();
        Assert.AreEqual(0, savedCount, "API results should NOT be persisted to the database");
    }
}
