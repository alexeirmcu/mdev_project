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
    public async Task Handle_WithLocalResults_ReturnsLocal_NoExternalCall()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new("fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, true, [], []),
            new("fsq-louvre-456", "Musée du Louvre", 2L,
                new PlaceLocationModel(48.8566, 2.3522), 180, true, true, true, [], []),
            new("fsq-colosseum-789", "Colosseum", 3L,
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
    public async Task Handle_WithCascadeResults_ReturnsMappedModelsTransparently()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new("fsq-prado-123", "Museo del Prado", 1L,
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
            new("fsq-api-1", "Museo del Prado", 1L,
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
        var request = new SearchPlacesRequest(new PlaceSearchRequest(null, "madrid-es", 5), 5);

        _repositoryMock
            .Setup(r => r.SearchAsync(null, "madrid-es", 5, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync(null, "madrid-es", It.IsAny<long>(), 5, It.IsAny<PlaceSearchFilter?>()))
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
            "fsq-gran-palace", "Gran Palace", 1L,
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
        var filter = new PlaceSearchFilter("Museum", true, true, 120);
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museum", "madrid-es", 10,
                Category: "Museum", IsIndoor: true, IsFamilyFriendly: true, MaxDurationMinutes: 120),
            10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == "Museum" &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120)))
            .ReturnsAsync(places);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.SearchAsync("Museum", "madrid-es", 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == "Museum" &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120)),
            Times.Once);
        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<PlaceSearchFilter?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_PassesFilterToExternalService()
    {
        var filter = new PlaceSearchFilter("Museum", true, true, 120);
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museum", "madrid-es", 10,
                Category: "Museum", IsIndoor: true, IsFamilyFriendly: true, MaxDurationMinutes: 120),
            10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10, It.IsAny<PlaceSearchFilter?>()))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museum", "madrid-es", It.IsAny<long>(), 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == "Museum" &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120)))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync("Museum", "madrid-es", It.IsAny<long>(), 10,
                It.Is<PlaceSearchFilter>(f =>
                    f.Category == "Museum" &&
                    f.IsIndoor == true &&
                    f.IsFamilyFriendly == true &&
                    f.MaxDurationMinutes == 120)),
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
