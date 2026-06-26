using AutoMapper;
using Moq;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Tests.Helpers;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class SearchPlacesHandlerTests
{
    private readonly Mock<IPlaceRepository> _repositoryMock = new();
    private readonly Mock<IPlaceExternalService> _externalServiceMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly SearchPlacesHandler _handler;

    public SearchPlacesHandlerTests()
    {
        _repositoryMock.Setup(r => r.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _handler = new SearchPlacesHandler(
            _repositoryMock.Object, _externalServiceMock.Object, _cityRepoMock.Object, _mapperMock.Object);
    }

    private void SetupEmptyMapper()
    {
        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(It.IsAny<List<Place>>()))
            .Returns([]);
    }

    private void SetUpCityRepo(string cityCode, bool allowed = true)
    {
        _cityRepoMock
            .Setup(r => r.GetByCodeAsync(cityCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new City(cityCode, cityCode, allowed));
    }

    private bool _fetchFromExternalIfInsufficient = true;

    private SearchPlacesRequest CreateRequest(string? query, string cityCode, int? maxResults = null,
        string? category = null, bool? isIndoor = null, bool? isFamilyFriendly = null,
        int? maxDurationMinutes = null, bool? fetchFromExternalIfInsufficient = null)
    {
        _fetchFromExternalIfInsufficient = fetchFromExternalIfInsufficient ?? true;
        return new SearchPlacesRequest(
            new PlaceSearchRequest(query, cityCode, maxResults,
                category, isIndoor, isFamilyFriendly, maxDurationMinutes, fetchFromExternalIfInsufficient),
            maxResults ?? 10);
    }

    private static List<Place> CreateThreePlaces()
    {
        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(48.8566, 2.3522);
        var loc3 = new PlaceLocation(41.9028, 12.4964);

        return
        [
            new Place("fsq-prado-123", "Museo del Prado", 1L, loc1, 120, true, false),
            new Place("fsq-louvre-456", "Musée du Louvre", 2L, loc2, 180, true, true),
            new Place("fsq-colosseum-789", "Colosseum", 3L, loc3, 90, false, true),
        ];
    }

    [TestMethod]
    public async Task Handle_WithLocalResults_CityNotFound_ReturnsLocalNoExternalCall()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new(1L, "fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, true, [], []),
            new(2L, "fsq-louvre-456", "Musée du Louvre", 2L,
                new PlaceLocationModel(48.8566, 2.3522), 180, true, true, true, [], []),
            new(3L, "fsq-colosseum-789", "Colosseum", 3L,
                new PlaceLocationModel(41.9028, 12.4964), 90, false, true, true, [], []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Results.Count);

        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<PlaceSearchFilter?>()),
            Times.Never);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_FetchFromExternalIfInsufficientFalse_LocalInsufficient_ReturnsLocal_NoExternalCall()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museum", "madrid-es", 10,
                FetchFromExternalIfInsufficient: false));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new(1L, "fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, true, [], []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);

        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<PlaceSearchFilter?>()),
            Times.Never);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_ColdStart_NoProviderIdForCategory_ReturnsLocal_NoExternalCall()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museum", "madrid-es", 10, Category: "Museum"));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(places);

        // GetProviderIdForCategoryAsync returns null → cold start
        _repositoryMock
            .Setup(r => r.GetProviderIdForCategoryAsync("Museum", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var mappedModels = new List<PlaceModel>
        {
            new(1L, "fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, true, [], []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);

        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<PlaceSearchFilter?>()),
            Times.Never);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_DedupMerge_PreservesEnrichmentFields()
    {
        // GIVEN local place with enrichment
        var loc = new PlaceLocation(40.4168, -3.7038);
        var localPlace = new Place("fsq-prado-123", "Museo del Prado", 1L, loc, 120, true, true);
        localPlace.MarkEnriched(120, true, 4, 0.9);
        var localPlaces = new List<Place> { localPlace };

        // AND external returns same ProviderReferenceId with different basic data
        var externalLoc = new PlaceLocation(40.4169, -3.7039);
        var externalPlace = new Place("fsq-prado-123", "Prado Museum (Updated)", 1L, externalLoc, 90, false, false);
        var externalPlaces = new List<Place> { externalPlace };

        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(localPlaces);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museum", "madrid-es", It.IsAny<long>(), 10,
                It.IsAny<PlaceSearchFilter?>(), It.IsAny<List<string>?>()))
            .ReturnsAsync(externalPlaces);

        // Capture the merged list that gets persisted
        List<Place>? capturedMerged = null;
        _repositoryMock
            .Setup(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()))
            .Callback<IEnumerable<Place>>(p => capturedMerged = p.ToList())
            .Returns(Task.CompletedTask);

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(It.IsAny<List<Place>>()))
            .Returns(new List<PlaceModel>());

        await _handler.Handle(request, CancellationToken.None);

        // THEN basic fields updated from external
        Assert.IsNotNull(capturedMerged);
        var merged = capturedMerged.Single();
        Assert.AreEqual("Prado Museum (Updated)", merged.Name);
        Assert.AreEqual(40.4169, merged.Location.Latitude);
        Assert.AreEqual(-3.7039, merged.Location.Longitude);

        // THEN enrichment fields preserved from local
        Assert.IsTrue(merged.IsEnriched);
        Assert.AreEqual(4, merged.FamilyFriendlyScore);
        Assert.AreEqual(0.9, merged.Popularity);

        // THEN enrichment causes local duration/indoor/familyFriendly to be preserved
        Assert.AreEqual(120, merged.TypicalDurationMinutes);
        Assert.IsTrue(merged.IsIndoor);
        Assert.IsTrue(merged.IsFamilyFriendly);
    }

    [TestMethod]
    public async Task Handle_WithCascadeResults_ReturnsMappedModelsTransparently()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new(1L, "fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, true, [], []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);
        Assert.AreEqual("fsq-prado-123", result.Results[0].ProviderReferenceId);
        _repositoryMock.Verify(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_CallsExternal_SavesToDb_ReturnsMapped()
    {
        var externalPlaces = new List<Place>
        {
            new("fsq-api-1", "Museo del Prado", 1L,
                new PlaceLocation(40.4168, -3.7038), 120, true, true)
        };
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museo", "madrid-es", 5));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museo", "madrid-es", 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", It.IsAny<long>(), 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(externalPlaces);

        var mappedModels = new List<PlaceModel>
        {
            new(1L, "fsq-api-1", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, true, true, [], [])
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(It.IsAny<List<Place>>()))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);
        Assert.AreEqual("fsq-api-1", result.Results[0].ProviderReferenceId);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(externalPlaces), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_ExternalError_ReturnsEmpty_NoSave()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museo", "madrid-es", 5));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museo", "madrid-es", 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", It.IsAny<long>(), 5, It.IsAny<PlaceSearchFilter?>()))
            .ThrowsAsync(new HttpRequestException("API down"));

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Results.Count);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_ExternalEmpty_ReturnsEmpty_NoSave()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museo", "madrid-es", 5));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museo", "madrid-es", 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", It.IsAny<long>(), 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Results.Count);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithLocalResults_DoesNotPersistExternalData()
    {
        var localPlaces = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(localPlaces);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<PlaceSearchFilter?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithEmptyLocalResults_ReturnsEmptyList()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("NonExistent", "nowhere", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("NonExistent", "nowhere", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        _cityRepoMock
            .Setup(r => r.GetByCodeAsync("nowhere", It.IsAny<CancellationToken>()))
            .ReturnsAsync((City?)null);

        SetupEmptyMapper();

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Results.Count);
    }

    [TestMethod]
    public async Task Handle_WithNullQuery_PassesNullToRepository()
    {
        var request = CreateRequest(null, "madrid-es", 5);

        _repositoryMock
            .Setup(r => r.SearchAsync(null, "madrid-es", 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        // Handler passes string.Empty to external when query is null
        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync(string.Empty, "madrid-es", It.IsAny<long>(), 5,
                It.IsAny<PlaceSearchFilter?>(), It.IsAny<List<string>?>()))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync(null, "madrid-es", 5, It.IsAny<PlaceSearchFilter?>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithDefaultMaxResults_UsesDefault()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Cafe", "madrid-es", null));

        _repositoryMock
            .Setup(r => r.SearchAsync("Cafe", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Cafe", "madrid-es", It.IsAny<long>(), 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync("Cafe", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithLocalResultsContainingAttributes_ReturnsMappedAttributes()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Hotel", "madrid-es", 10), 10);

        var loc = new PlaceLocation(40.4168, -3.7038);
        var place = new Place("fsq-gran-palace", "Gran Palace", 1L, loc, 120, true, true);
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"));

        _repositoryMock
            .Setup(r => r.SearchAsync("Hotel", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(new List<Place> { place });

        var mappedPlace = new PlaceModel(
            1L, "fsq-gran-palace", "Gran Palace", 1L,
            new PlaceLocationModel(40.4168, -3.7038), 120, true, true, true,
            new List<OpeningHoursWindowModel>(),
            new List<PlaceAttributeModel>
            {
                new("category", "Hotel")
            });

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(It.IsAny<List<Place>>()))
            .Returns(new List<PlaceModel> { mappedPlace });

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);

        var model = result.Results[0];
        Assert.AreEqual("Gran Palace", model.Name);
        Assert.AreEqual(1, model.Attributes.Count);
        Assert.AreEqual("category", model.Attributes[0].Key);
        Assert.AreEqual("Hotel", model.Attributes[0].Value);
    }

    [TestMethod]
    public async Task Handle_WithLocalResults_PassesFilterToRepository()
    {
        var places = CreateThreePlaces();
        var request = CreateRequest("Museum", "madrid-es", 10,
            category: "Museum", isIndoor: true, isFamilyFriendly: true, maxDurationMinutes: 120);

        // Handler now creates filter with Category=null (category handled via SearchAsync string param)
        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == null &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120)))
            .ReturnsAsync(places);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.SearchAsync("Museum", "madrid-es", 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == null &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120)),
            Times.Once);
        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<PlaceSearchFilter?>(), It.IsAny<List<string>?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_PassesFilterToExternalService()
    {
        var request = CreateRequest("Museum", "madrid-es", 10,
            category: "Museum", isIndoor: true, isFamilyFriendly: true, maxDurationMinutes: 120);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        // Category="Museum" triggers category resolution
        _repositoryMock
            .Setup(r => r.GetProviderIdForCategoryAsync("Museum", It.IsAny<CancellationToken>()))
            .ReturnsAsync("10000");

        // Handler creates filter with Category=null
        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museum", "madrid-es", It.IsAny<long>(), 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == null &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120),
                It.IsAny<List<string>?>()))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync("Museum", "madrid-es", It.IsAny<long>(), 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == null &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120),
                It.IsAny<List<string>?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_FilterWithNulls_PassesNullFilter()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museum", "madrid-es", 10,
                Category: null, IsIndoor: null, IsFamilyFriendly: null, MaxDurationMinutes: null));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == null &&
                    f.IsIndoor == null &&
                    f.IsFamilyFriendly == null &&
                    f.MaxDurationMinutes == null)))
            .ReturnsAsync(places);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.SearchAsync("Museum", "madrid-es", 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == null &&
                    f.IsIndoor == null &&
                    f.IsFamilyFriendly == null &&
                    f.MaxDurationMinutes == null)),
            Times.Once);
    }
}
