using Moq;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Mapping;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Foursquare;

[TestClass]
public sealed class FoursquarePlaceServiceTests
{
    private static FoursquarePlace CreateMuseumPlace()
    {
        return new FoursquarePlace
        {
            FsqPlaceId = "fsq_museum_1",
            Name = "Museo del Prado",
            Latitude = 40.4168,
            Longitude = -3.7038,
            Categories = new List<FoursquareCategory>
            {
                new() { FsqCategoryId = "10000", Name = "Museum" }
            }
        };
    }

    private static FoursquarePlace CreateNightclubPlace()
    {
        return new FoursquarePlace
        {
            FsqPlaceId = "fsq_night_1",
            Name = "Teatro de la Luz",
            Latitude = 40.4169,
            Longitude = -3.7039,
            Categories = new List<FoursquareCategory>
            {
                new() { FsqCategoryId = "10008", Name = "Nightclub" }
            }
        };
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithValidApiResponse_ReturnsMappedPlaces()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateMuseumPlace(),
            CreateNightclubPlace()
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 20);

        // Assert
        Assert.AreEqual(2, results.Count);

        var first = results[0];
        Assert.AreEqual("fsq_museum_1", first.PlaceId);
        Assert.AreEqual("Museo del Prado", first.Name);
        Assert.AreEqual("madrid-es", first.CityId);
        Assert.AreEqual(40.4168, first.Location.Latitude);
        Assert.AreEqual(-3.7038, first.Location.Longitude);
        Assert.AreEqual(120, first.TypicalDurationMinutes);
        Assert.IsTrue(first.IsIndoor);
        Assert.IsTrue(first.IsFamilyFriendly);

        var second = results[1];
        Assert.AreEqual("fsq_night_1", second.PlaceId);
        Assert.AreEqual("Teatro de la Luz", second.Name);
        Assert.AreEqual(60, second.TypicalDurationMinutes);
        Assert.IsFalse(second.IsFamilyFriendly);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithZeroLocation_ReturnsPlaceWithZeroLocation()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            new()
            {
                FsqPlaceId = "fsq_no_geo",
                Name = "No Geo Place",
                Latitude = 0,
                Longitude = 0,
                Categories = new List<FoursquareCategory>()
            }
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("NoGeo", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("NoGeo", "madrid-es", 20);

        // Assert
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(0, results[0].Location.Latitude);
        Assert.AreEqual(0, results[0].Location.Longitude);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithHttpRequestException_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ThrowsAsync(new HttpRequestException("API error"));

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 20);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithApiReturningEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        mockClient
            .Setup(c => c.SearchPlacesAsync("Empty", "madrid-es", 20))
            .ReturnsAsync(new List<FoursquarePlace>());

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("Empty", "madrid-es", 20);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }
}
