using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class GenerateTripHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<ITripCodeGenerator> _codeGenMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly GenerateTripHandler _handler;

    public GenerateTripHandlerTests()
    {
        _handler = new GenerateTripHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _placeRepoMock.Object,
            _codeGenMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<GenerateTripHandler>>());
    }

    private static TripGenerationRequest CreateValidRequest(int? pinnedDayIndex = null)
    {
        return new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput>
            {
                new(1L, Priority.High, pinnedDayIndex),
                new(2L, Priority.Medium)
            },
            new TravelersInput(2, 1, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");
    }

    [TestMethod]
    public async Task Handle_ValidRequest_ReturnsTripPlanResponse()
    {
        // Arrange
        var request = CreateValidRequest();
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>
            {
                CreatePlace(1L),
                CreatePlace(2L)
            });

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-X7K9");

        // Act
        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreNotEqual(Guid.Empty, result.TripId);
        Assert.AreEqual("MAD-2026-X7K9", result.TripCode);
        Assert.AreEqual(1L, result.CityId);
        Assert.AreEqual("madrid-es", result.CityCode);
        Assert.AreEqual("Madrid", result.CityName);
        Assert.AreEqual(new DateOnly(2026, 7, 1), result.StartDate);
        Assert.AreEqual(new DateOnly(2026, 7, 3), result.EndDate);
        Assert.AreEqual(2, result.Travelers.Adults);
        Assert.AreEqual(1, result.Travelers.Children);
        Assert.AreEqual(2, result.MustSees.Count);
        Assert.AreEqual("CREATED", result.Status);

        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_CityNotFound_ThrowsCityNotFoundException()
    {
        var request = CreateValidRequest();
        _cityRepoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((City?)null);

        var exception = await CatchExceptionAsync<CityNotFoundException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_CityNotAllowed_ThrowsBusinessRuleException()
    {
        var request = CreateValidRequest();
        var city = new City("madrid-es", "Madrid", false);
        SetEntityId(city, 1L);

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_PlaceIdNotFound_ThrowsBusinessRuleException()
    {
        var request = CreateValidRequest();
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        // Only return 1 of the 2 requested place IDs
        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlace(1L) });

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_PinnedDayOutOfRange_ThrowsBusinessRuleException()
    {
        // 3-day trip (Jul 1-3), pinned day 5 is out of range [0, 2]
        var request = CreateValidRequest(pinnedDayIndex: 5);
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlace(1L), CreatePlace(2L) });

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_DurationExceedsMax_ThrowsBusinessRuleException()
    {
        // 15-day trip exceeds 14-day max
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 15),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput> { new(1L, Priority.High) },
            null, null, "09:00");

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlace(1L) });

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_PinnedBlockWithoutPinnedDayIndex_ThrowsBusinessRuleException()
    {
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput> { new(1L, Priority.High, null, BlockType.Morning) },
            null, null, "09:00");

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlace(1L) });

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(exception);
        StringAssert.Contains(exception.Message, "PinnedBlock cannot be set without PinnedDayIndex");
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void SetEntityId<T>(T entity, long id) where T : class
    {
        var entityType = typeof(global::SmartTripPlanner.Domain.Base.Entity);
        var field = entityType.GetField("_Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(entity, id);
    }

    private static async Task<T?> CatchExceptionAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
            return null;
        }
        catch (T ex)
        {
            return ex;
        }
    }

    private static Place CreatePlace(long id)
    {
        var place = new Place($"fsq-{id}", $"Place {id}", 1L, new PlaceLocation(0, 0));
        SetEntityId(place, id);
        return place;
    }
}
