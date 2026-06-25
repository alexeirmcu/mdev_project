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
public sealed class RegenerateDayHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<IItineraryReplanningEngine> _engineMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly IMapper _mapper;
    private readonly RegenerateDayHandler _handler;

    public RegenerateDayHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _handler = new RegenerateDayHandler(
            _tripRepoMock.Object,
            _placeRepoMock.Object,
            _weatherProviderMock.Object,
            _engineMock.Object,
            _mapper,
            Mock.Of<ILogger<RegenerateDayHandler>>(),
            _userContextMock.Object);
    }

    private static Trip CreateTrip(string ownerUserId = "user-42")
    {
        var city = new City("madrid-es", "Madrid", true);
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-TEST",
            CityId = 1L,
            City = city,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 3),
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

        _engineMock.Setup(e => e.RegenerateDayAsync(
                trip, 1, places, weather, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(
            new RegenerateDay(tripId, 1, "user-42"), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(tripId, result.TripId);
        Assert.AreEqual("GENERATED", result.Status);

        _engineMock.Verify(e => e.RegenerateDayAsync(
            trip, 1, places, weather, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new RegenerateDay(tripId, 0, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WrongOwner_ThrowsTripForbiddenException()
    {
        var trip = CreateTrip("user-99");
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<TripForbiddenException>(
            () => _handler.Handle(new RegenerateDay(tripId, 0, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_DayIndexOutOfRange_ThrowsDayNotFoundException()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<DayNotFoundException>(
            () => _handler.Handle(new RegenerateDay(tripId, 5, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_NegativeDayIndex_ThrowsDayNotFoundException()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<DayNotFoundException>(
            () => _handler.Handle(new RegenerateDay(tripId, -1, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public async Task Handle_NoDaysGenerated_ThrowsBusinessRuleException()
    {
        var city = new City("madrid-es", "Madrid", true);
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-NOD",
            CityId = 1L,
            City = city,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 3),
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

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new RegenerateDay(tripId, 0, "user-42"), CancellationToken.None));

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
