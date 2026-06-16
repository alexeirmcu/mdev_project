using AutoMapper;
using Moq;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

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
            .Setup(r => r.GetByCodeAsync(cityCode))
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
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new("fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, []),
            new("fsq-louvre-456", "Musée du Louvre", 2L,
                new PlaceLocationModel(48.8566, 2.3522), 180, true, true, []),
            new("fsq-colosseum-789", "Colosseum", 3L,
                new PlaceLocationModel(41.9028, 12.4964), 90, false, true, []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Results.Count);

        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()),
            Times.Never);

        _repositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithCascadeResults_ReturnsMappedModelsTransparently()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new("fsq-prado-123", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);
        Assert.AreEqual("fsq-prado-123", result.Results[0].ProviderReferenceId);
        _repositoryMock.Verify(r => r.SearchAsync("Museum", "madrid-es", 10), Times.Once);
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
            .Setup(r => r.SearchAsync("Museo", "madrid-es", 5))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", It.IsAny<long>(), 5))
            .ReturnsAsync(externalPlaces);

        var mappedModels = new List<PlaceModel>
        {
            new("fsq-api-1", "Museo del Prado", 1L,
                new PlaceLocationModel(40.4168, -3.7038), 120, true, true, [])
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(It.IsAny<List<Place>>()))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);
        Assert.AreEqual("fsq-api-1", result.Results[0].ProviderReferenceId);

        _repositoryMock.Verify(r => r.AddRangeAsync(externalPlaces), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_ExternalError_ReturnsEmpty_NoSave()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museo", "madrid-es", 5));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museo", "madrid-es", 5))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", It.IsAny<long>(), 5))
            .ThrowsAsync(new HttpRequestException("API down"));

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Results.Count);

        _repositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_NoLocalResults_ExternalEmpty_ReturnsEmpty_NoSave()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museo", "madrid-es", 5));

        _repositoryMock
            .Setup(r => r.SearchAsync("Museo", "madrid-es", 5))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Museo", "madrid-es", It.IsAny<long>(), 5))
            .ReturnsAsync([]);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Results.Count);

        _repositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithLocalResults_DoesNotPersistExternalData()
    {
        var localPlaces = CreateThreePlaces();
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Museum", "madrid-es", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10))
            .ReturnsAsync(localPlaces);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Place>>()), Times.Never);
        _externalServiceMock.Verify(
            s => s.SearchPlacesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithEmptyLocalResults_ReturnsEmptyList()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("NonExistent", "nowhere", 10), 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("NonExistent", "nowhere", 10))
            .ReturnsAsync([]);

        _cityRepoMock
            .Setup(r => r.GetByCodeAsync("nowhere"))
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
            .Setup(r => r.SearchAsync(null, "madrid-es", 5))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync(null, "madrid-es", It.IsAny<long>(), 5))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync(null, "madrid-es", 5), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithDefaultMaxResults_UsesDefault()
    {
        var request = new SearchPlacesRequest(new PlaceSearchRequest("Cafe", "madrid-es", null));

        _repositoryMock
            .Setup(r => r.SearchAsync("Cafe", "madrid-es", 10))
            .ReturnsAsync([]);

        SetUpCityRepo("madrid-es");

        _externalServiceMock
            .Setup(s => s.SearchPlacesAsync("Cafe", "madrid-es", It.IsAny<long>(), 10))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync("Cafe", "madrid-es", 10), Times.Once);
    }
}
