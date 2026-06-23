using Moq;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
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

    private static FoursquarePlace CreateChainPlace()
    {
        return new FoursquarePlace
        {
            FsqPlaceId = "fsq_mcd_1",
            Name = "McDonald's Centro",
            Latitude = 40.4168,
            Longitude = -3.7038,
            Categories = new List<FoursquareCategory>
            {
                new() { FsqCategoryId = "13002", Name = "Fast Food" }
            },
            Chains = new List<FoursquareChain>
            {
                new() { Id = "123", Name = "McDonald's" }
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
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20);

        // Assert
        Assert.AreEqual(2, results.Count);

        var first = results[0];
        Assert.AreEqual("fsq_museum_1", first.ProviderReferenceId);
        Assert.AreEqual("Museo del Prado", first.Name);
        Assert.AreEqual(1L, first.CityId);
        Assert.AreEqual(40.4168, first.Location.Latitude);
        Assert.AreEqual(-3.7038, first.Location.Longitude);
        Assert.AreEqual(120, first.TypicalDurationMinutes);
        Assert.IsTrue(first.IsIndoor);
        Assert.IsTrue(first.IsFamilyFriendly);
        Assert.AreEqual(1, first.Attributes.Count);
        Assert.AreEqual("foursquare", first.Attributes.First().Provider);
        Assert.AreEqual("category", first.Attributes.First().Key);
        Assert.AreEqual("Museum", first.Attributes.First().Value);

        var second = results[1];
        Assert.AreEqual("fsq_night_1", second.ProviderReferenceId);
        Assert.AreEqual("Teatro de la Luz", second.Name);
        Assert.AreEqual(60, second.TypicalDurationMinutes);
        Assert.IsFalse(second.IsFamilyFriendly);
        Assert.AreEqual(1, second.Attributes.Count);
        Assert.AreEqual("foursquare", second.Attributes.First().Provider);
        Assert.AreEqual("category", second.Attributes.First().Key);
        Assert.AreEqual("Nightclub", second.Attributes.First().Value);
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
        var results = await service.SearchPlacesAsync("NoGeo", "madrid-es", 1L, 20);

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
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20);

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
        var results = await service.SearchPlacesAsync("Empty", "madrid-es", 1L, 20);

        // Assert
        Assert.IsNotNull(results);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithChainAttribute_MapsChainToAttribute()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateChainPlace()
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("McDonald's", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("McDonald's", "madrid-es", 1L, 20);

        // Assert
        Assert.AreEqual(1, results.Count);
        var place = results[0];
        Assert.AreEqual(2, place.Attributes.Count);

        var chainAttr = place.Attributes.First(a => a.Key == "chain");
        Assert.AreEqual("foursquare", chainAttr.Provider);
        Assert.AreEqual("chain", chainAttr.Key);
        Assert.AreEqual("McDonald's", chainAttr.Value);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithEmptyChains_SkipsChainAttributes()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            new()
            {
                FsqPlaceId = "fsq_no_chain",
                Name = "Local Restaurant",
                Latitude = 40.4168,
                Longitude = -3.7038,
                Categories = new List<FoursquareCategory>
                {
                    new() { FsqCategoryId = "13002", Name = "Restaurant" }
                },
                Chains = new List<FoursquareChain>()
            }
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Restaurant", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("Restaurant", "madrid-es", 1L, 20);

        // Assert
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(1, results[0].Attributes.Count);
        Assert.AreEqual("category", results[0].Attributes.First().Key);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_WithValidPlace_InjectsDefaultOpeningHours()
    {
        // Arrange
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace> { CreateMuseumPlace() };
        mockClient
            .Setup(c => c.SearchPlacesAsync("OpeningHours", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);

        // Act
        var results = await service.SearchPlacesAsync("OpeningHours", "madrid-es", 1L, 20);

        // Assert
        Assert.AreEqual(1, results.Count);
        var place = results[0];
        Assert.AreEqual(7, place.OpeningHours.Count);

        var expectedDays = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

        for (int i = 0; i < 7; i++)
        {
            Assert.AreEqual(expectedDays[i], place.OpeningHours[i].DayOfWeek);
            Assert.AreEqual(540, place.OpeningHours[i].OpenMinutes);
            Assert.AreEqual(1080, place.OpeningHours[i].CloseMinutes);
        }
    }

    [TestMethod]
    public async Task SearchPlacesAsync_FilterByIsFamilyFriendly_FiltersClientSide()
    {
        // Note: All categories in FoursquareCategoryHeuristics map IsIndoor=true,
        // so we test IsFamilyFriendly which differs between Museum (true) and Nightclub (false)
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateMuseumPlace(),   // IsFamilyFriendly = true
            CreateNightclubPlace() // IsFamilyFriendly = false
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);
        var filter = new PlaceSearchFilter(null, null, true, null);
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20, filter);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Museo del Prado", results[0].Name);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_FilterByIsNotFamilyFriendly_FiltersClientSide()
    {
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateMuseumPlace(),   // IsFamilyFriendly = true
            CreateNightclubPlace() // IsFamilyFriendly = false
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);
        var filter = new PlaceSearchFilter(null, null, false, null);
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20, filter);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Teatro de la Luz", results[0].Name);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_FilterByMaxDuration_FiltersClientSide()
    {
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateMuseumPlace(),   // duration 120
            CreateNightclubPlace() // duration 60
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);
        var filter = new PlaceSearchFilter(null, null, null, 60);
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20, filter);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Teatro de la Luz", results[0].Name);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_FilterByCategory_FiltersClientSide()
    {
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateMuseumPlace(),   // category = "Museum"
            CreateNightclubPlace() // category = "Nightclub"
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);
        var filter = new PlaceSearchFilter("Museum", null, null, null);
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20, filter);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Museo del Prado", results[0].Name);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_FilterNull_PreservesExistingBehavior()
    {
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
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task SearchPlacesAsync_FilterMultiple_AppliesAllFilters()
    {
        var mockClient = new Mock<IFoursquareApiClient>();
        var apiPlaces = new List<FoursquarePlace>
        {
            CreateMuseumPlace(),   // IsFamilyFriendly=true, duration=120, cat=Museum
            CreateNightclubPlace() // IsFamilyFriendly=false, duration=60, cat=Nightclub
        };
        mockClient
            .Setup(c => c.SearchPlacesAsync("Museum", "madrid-es", 20))
            .ReturnsAsync(apiPlaces);

        var service = new FoursquarePlaceService(mockClient.Object);
        var filter = new PlaceSearchFilter("Museum", null, true, 120);
        var results = await service.SearchPlacesAsync("Museum", "madrid-es", 1L, 20, filter);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Museo del Prado", results[0].Name);
    }
}
