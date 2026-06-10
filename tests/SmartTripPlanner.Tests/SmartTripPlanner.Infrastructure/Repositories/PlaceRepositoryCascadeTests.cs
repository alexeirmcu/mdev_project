using Microsoft.EntityFrameworkCore;
using Moq;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure;
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

    [TestMethod]
    public async Task SearchAsync_LocalResultsExist_ReturnsLocal_NoApiCall()
    {
        // Arrange
        using var db = CreateDbContext();
        db.Places.Add(new Place("f1", "Museo del Prado", "madrid-es", new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Museo Reina Sofia", "madrid-es", new PlaceLocation(40.4089, -3.6944)));
        await db.SaveChangesAsync();

        var mockService = new Mock<IPlaceExternalService>(MockBehavior.Strict);
        var repo = new PlaceRepository(db, mockService.Object);

        // Act
        var results = await repo.SearchAsync("Museo", "madrid-es");

        // Assert
        Assert.AreEqual(2, results.Count);
        mockService.Verify(s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SearchAsync_NoLocalResults_CallsApi_ReturnsMapped()
    {
        // Arrange
        using var db = CreateDbContext();
        var apiPlaces = new List<Place>
        {
            new Place("fsq1", "Museo del Prado", "madrid-es",
                new PlaceLocation(40.4168, -3.7038), 120, true, true)
        };
        var mockService = new Mock<IPlaceExternalService>();
        mockService
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var repo = new PlaceRepository(db, mockService.Object);

        // Act
        var results = await repo.SearchAsync("Museo", "madrid-es");

        // Assert
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("fsq1", results[0].PlaceId);
        Assert.AreEqual("Museo del Prado", results[0].Name);
        Assert.AreEqual("madrid-es", results[0].CityId);
        mockService.Verify(s => s.SearchPlacesAsync("Museo", "madrid-es", 20), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_NoLocalResults_ApiError_ReturnsEmpty()
    {
        // Arrange
        using var db = CreateDbContext();
        var mockService = new Mock<IPlaceExternalService>();
        mockService
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", 20))
            .ThrowsAsync(new HttpRequestException("API error"));

        var repo = new PlaceRepository(db, mockService.Object);

        // Act
        var results = await repo.SearchAsync("Museo", "madrid-es");

        // Assert
        Assert.AreEqual(0, results.Count);
        mockService.Verify(s => s.SearchPlacesAsync("Museo", "madrid-es", 20), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_SavedPlaces_NotPersistedFromApi()
    {
        // Arrange
        using var db = CreateDbContext();
        var apiPlaces = new List<Place>
        {
            new Place("fsq_api_1", "Prado from API", "madrid-es",
                new PlaceLocation(40.4168, -3.7038), 120, true, true)
        };
        var mockService = new Mock<IPlaceExternalService>();
        mockService
            .Setup(s => s.SearchPlacesAsync("Prado", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var repo = new PlaceRepository(db, mockService.Object);

        // Act
        var results = await repo.SearchAsync("Prado", "madrid-es");

        // Assert
        Assert.AreEqual(1, results.Count);

        var savedCount = await db.Places.CountAsync();
        Assert.AreEqual(0, savedCount, "API results should NOT be persisted to the database");
    }
}
