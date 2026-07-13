using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartTripPlanner.API.Configurations;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class TripSmartReplanHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<IItineraryReplanningEngine> _engineMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly IMapper _mapper;
    private readonly TripSmartReplanHandler _handler;

    public TripSmartReplanHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _handler = new TripSmartReplanHandler(
            _tripRepoMock.Object,
            _placeRepoMock.Object,
            _weatherProviderMock.Object,
            _engineMock.Object,
            _mapper,
            Mock.Of<ILogger<TripSmartReplanHandler>>(),
            _userContextMock.Object);
    }

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip(string ownerUserId = "user-42")
    {
        var city = new City("madrid-es", "Madrid", true);
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-TEST",
            CityId = 1L,
            City = city,
            StartDate = FutureStartDate,
            EndDate = FutureStartDate.AddDays(2),
            BaseHotel = new Location("Hotel Central", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        trip.GenerateDaysFrom(trip.StartDate);
        return trip;
    }

    [TestMethod]
    public async Task Handle_ValidRequest_DelegatesToEngineAndUpdates()
    {
        // Arrange
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var places = new List<Place>
        {
            new Place("fsq-1", "Place 1", 1L, new PlaceLocation(0, 0)),
            new Place("fsq-2", "Place 2", 1L, new PlaceLocation(0, 0))
        };

        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                1L, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(places);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Clear },
            { trip.Days[1].Date, WeatherCondition.Good },
            { trip.Days[2].Date, WeatherCondition.Bad }
        };

        _weatherProviderMock.Setup(w => w.GetWeatherAsync(
                1L, trip.StartDate, trip.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weather);

        _engineMock.Setup(e => e.ReplanAsync(
                trip, It.IsAny<ReplanContext>(), places, weather, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - morning of day 1 (July 2), scope CurrentDay, good weather
        var payload = new TripSmartReplanRequest(
            DateTime.SpecifyKind(FutureStartDate.ToDateTime(new TimeOnly(9, 0)).AddDays(1), DateTimeKind.Utc),
            "CurrentDay",
            WeatherCondition.Good);
        var result = await _handler.Handle(
            new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(tripId, result.TripId);

        _engineMock.Verify(e => e.ReplanAsync(
            trip,
            It.Is<ReplanContext>(ctx =>
                ctx.CurrentDayIndex == 1 &&
                ctx.CurrentBlock == BlockType.Morning &&
                ctx.Scope == ReplanScope.CurrentDay &&
                ctx.IsBadWeather == false),
            places, weather, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_Afternoon_ResolvesBlockCorrectly()
    {
        // Arrange
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>());

        _weatherProviderMock.Setup(w => w.GetWeatherAsync(
                It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateOnly, WeatherCondition>());

        _engineMock.Setup(e => e.ReplanAsync(
                It.IsAny<Trip>(), It.IsAny<ReplanContext>(),
                It.IsAny<IReadOnlyList<Place>>(),
                It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - afternoon of day 0 (July 1), scope RemainingTrip, bad weather
        var payload = new TripSmartReplanRequest(
            DateTime.SpecifyKind(FutureStartDate.ToDateTime(new TimeOnly(14, 30)), DateTimeKind.Utc),
            "RemainingTrip",
            WeatherCondition.Bad);
        var result = await _handler.Handle(
            new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);

        _engineMock.Verify(e => e.ReplanAsync(
            trip,
            It.Is<ReplanContext>(ctx =>
                ctx.CurrentDayIndex == 0 &&
                ctx.CurrentBlock == BlockType.Afternoon &&
                ctx.Scope == ReplanScope.RemainingTrip &&
                ctx.IsBadWeather == true),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_Evening_ResolvesBlockCorrectly()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>());
        _weatherProviderMock.Setup(w => w.GetWeatherAsync(
                It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateOnly, WeatherCondition>());
        _engineMock.Setup(e => e.ReplanAsync(
                It.IsAny<Trip>(), It.IsAny<ReplanContext>(),
                It.IsAny<IReadOnlyList<Place>>(),
                It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var payload = new TripSmartReplanRequest(
            DateTime.SpecifyKind(FutureStartDate.ToDateTime(new TimeOnly(20, 0)), DateTimeKind.Utc),
            "CurrentBlock",
            WeatherCondition.Good);
        var result = await _handler.Handle(
            new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None);

        Assert.IsNotNull(result);
        _engineMock.Verify(e => e.ReplanAsync(
            trip,
            It.Is<ReplanContext>(ctx =>
                ctx.CurrentDayIndex == 0 &&
                ctx.CurrentBlock == BlockType.Evening &&
                ctx.Scope == ReplanScope.CurrentBlock),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_AfterTripEnd_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // CurrentDateTime after trip end
        var payload = new TripSmartReplanRequest(
            DateTime.SpecifyKind(FutureStartDate.ToDateTime(new TimeOnly(10, 0)).AddDays(3), DateTimeKind.Utc),
            "CurrentDay",
            WeatherCondition.Good);

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
        StringAssert.Contains(exception!.Message, "after the trip end");
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var payload = new TripSmartReplanRequest(DateTime.UtcNow, "CurrentDay", WeatherCondition.Good);
        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public async Task Handle_WrongOwner_ThrowsTripForbiddenException()
    {
        var trip = CreateTrip("user-99");
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var payload = new TripSmartReplanRequest(DateTime.UtcNow, "CurrentDay", WeatherCondition.Good);
        var exception = await CatchExceptionAsync<TripForbiddenException>(
            () => _handler.Handle(new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public async Task Handle_BeforeTripStart_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // CurrentDateTime before trip start (June 30, trip starts July 1)
        var payload = new TripSmartReplanRequest(
            DateTime.SpecifyKind(FutureStartDate.ToDateTime(new TimeOnly(10, 0)).AddDays(-1), DateTimeKind.Utc),
            "CurrentDay",
            WeatherCondition.Good);

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
        StringAssert.Contains(exception!.Message, "hasn't started yet");
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_NoDays_ThrowsBusinessRuleException()
    {
        // Arrange - trip with no days (itinerary not generated)
        var city = new City("madrid-es", "Madrid", true);
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-NOD",
            CityId = 1L,
            City = city,
            StartDate = FutureStartDate,
            EndDate = FutureStartDate.AddDays(2),
            BaseHotel = new Location("Hotel Central", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };
        // No days generated
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var payload = new TripSmartReplanRequest(
            DateTime.SpecifyKind(FutureStartDate.ToDateTime(new TimeOnly(10, 0)), DateTimeKind.Utc),
            "CurrentDay",
            WeatherCondition.Good);

        // Act
        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new TripSmartReplan(tripId, payload, "user-42"), CancellationToken.None));

        // Assert
        Assert.IsNotNull(exception);
        StringAssert.Contains(exception!.Message, "Itinerary not generated");
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
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
}
