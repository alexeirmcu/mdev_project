using AutoMapper;
using Moq;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class SearchPlacesHandlerTests
{
    private readonly Mock<IPlaceRepository> _repositoryMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly SearchPlacesHandler _handler;

    public SearchPlacesHandlerTests()
    {
        _handler = new SearchPlacesHandler(_repositoryMock.Object, _mapperMock.Object);
    }

    private void SetupEmptyMapper()
    {
        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(It.IsAny<List<Place>>()))
            .Returns([]);
    }

    private static List<Place> CreateThreePlaces()
    {
        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(48.8566, 2.3522);
        var loc3 = new PlaceLocation(41.9028, 12.4964);

        return
        [
            new Place("fsq-prado-123", "Museo del Prado", "madrid-es", loc1, 120, true, false),
            new Place("fsq-louvre-456", "Musée du Louvre", "paris-fr", loc2, 180, true, true),
            new Place("fsq-colosseum-789", "Colosseum", "rome-it", loc3, 90, false, true),
        ];
    }

    [TestMethod]
    public async Task Handle_WithThreeLocalResults_ReturnsThreeMappedModels()
    {
        var places = CreateThreePlaces();
        var request = new SearchPlacesRequest("Museum", "madrid-es", 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("Museum", "madrid-es", 10))
            .ReturnsAsync(places);

        var mappedModels = new List<PlaceModel>
        {
            new("fsq-prado-123", "Museo del Prado", "madrid-es",
                new PlaceLocationModel(40.4168, -3.7038), 120, true, false, []),
            new("fsq-louvre-456", "Musée du Louvre", "paris-fr",
                new PlaceLocationModel(48.8566, 2.3522), 180, true, true, []),
            new("fsq-colosseum-789", "Colosseum", "rome-it",
                new PlaceLocationModel(41.9028, 12.4964), 90, false, true, []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mappedModels);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Results.Count);
        Assert.AreEqual("fsq-prado-123", result.Results[0].PlaceId);
        Assert.AreEqual("Museo del Prado", result.Results[0].Name);
        Assert.AreEqual("fsq-louvre-456", result.Results[1].PlaceId);
        Assert.AreEqual("fsq-colosseum-789", result.Results[2].PlaceId);
    }

    [TestMethod]
    public async Task Handle_WithCascadeResults_ReturnsMappedModelsTransparently()
    {
        var places = new List<Place>
        {
            new("fsq-pompidou-111", "Centre Pompidou", "paris-fr",
                new PlaceLocation(48.8606, 2.3522), 150, true, true),
        };
        var request = new SearchPlacesRequest("Art", "paris-fr");

        _repositoryMock
            .Setup(r => r.SearchAsync("Art", "paris-fr", 20))
            .ReturnsAsync(places);

        var mapped = new List<PlaceModel>
        {
            new("fsq-pompidou-111", "Centre Pompidou", "paris-fr",
                new PlaceLocationModel(48.8606, 2.3522), 150, true, true, []),
        };

        _mapperMock
            .Setup(m => m.Map<List<PlaceModel>>(places))
            .Returns(mapped);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Results.Count);
        Assert.AreEqual("fsq-pompidou-111", result.Results[0].PlaceId);
        Assert.AreEqual("Centre Pompidou", result.Results[0].Name);

        // Verify the repository was called (real call, not filtered by handler)
        _repositoryMock.Verify(r => r.SearchAsync("Art", "paris-fr", 20), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithEmptyResults_ReturnsEmptyList()
    {
        var request = new SearchPlacesRequest("NonExistent", "nowhere", 10);

        _repositoryMock
            .Setup(r => r.SearchAsync("NonExistent", "nowhere", 10))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Results.Count);
    }

    [TestMethod]
    public async Task Handle_WithNullQuery_PassesNullToRepository()
    {
        var request = new SearchPlacesRequest(null, "madrid-es", 5);

        _repositoryMock
            .Setup(r => r.SearchAsync(null, "madrid-es", 5))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync(null, "madrid-es", 5), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithDefaultMaxResults_Uses20()
    {
        var request = new SearchPlacesRequest("Cafe", "madrid-es");

        _repositoryMock
            .Setup(r => r.SearchAsync("Cafe", "madrid-es", 20))
            .ReturnsAsync([]);

        SetupEmptyMapper();

        await _handler.Handle(request, CancellationToken.None);

        _repositoryMock.Verify(r => r.SearchAsync("Cafe", "madrid-es", 20), Times.Once);
    }
}
