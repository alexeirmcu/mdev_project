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
public sealed class RefreshWeatherHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly IMapper _mapper;
    private readonly RefreshWeatherHandler _handler;

    public RefreshWeatherHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _handler = new RefreshWeatherHandler(
            _tripRepoMock.Object,
            _weatherProviderMock.Object,
            _mapper,
            Mock.Of<ILogger<RefreshWeatherHandler>>(),
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
    public async Task Handle_WeatherChanged_MarksStaleAndUpdates()
    {
        // Arrange
        var trip = CreateTrip();
        var tripId = trip.TripId;
        trip.Days[0].SetWeather(WeatherCondition.Clear);

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // Set days 1 and 2 to match the expected forecast to isolate change to day 0
        trip.Days[1].SetWeather(WeatherCondition.Good);
        trip.Days[2].SetWeather(WeatherCondition.Clear);

        // Weather provider returns different weather only for day 0
        _weatherProviderMock.Setup(w => w.GetWeatherAsync(1L, trip.StartDate, trip.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateOnly, WeatherCondition>
            {
                { trip.Days[0].Date, WeatherCondition.Bad },
                { trip.Days[1].Date, WeatherCondition.Good },
                { trip.Days[2].Date, WeatherCondition.Clear }
            });

        // Act
        var result = await _handler.Handle(new RefreshWeather(tripId, "user-42"), CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Updated);
        Assert.AreEqual(1, result.DaysRefreshed);
        Assert.AreEqual(1, result.Changes.Count);
        Assert.AreEqual(0, result.Changes[0].DayIndex);
        Assert.AreEqual("Clear", result.Changes[0].PreviousWeather);
        Assert.AreEqual("Bad", result.Changes[0].NewWeather);

        Assert.IsTrue(trip.Days[0].IsStale);
        Assert.IsFalse(trip.Days[1].IsStale);
        Assert.IsFalse(trip.Days[2].IsStale);

        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WeatherUnchanged_DoesNotUpdate()
    {
        // Arrange
        var trip = CreateTrip();
        var tripId = trip.TripId;
        trip.Days[0].SetWeather(WeatherCondition.Clear);

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // Set ALL days to match the forecast exactly so nothing changes
        trip.Days[1].SetWeather(WeatherCondition.Good);

        _weatherProviderMock.Setup(w => w.GetWeatherAsync(1L, trip.StartDate, trip.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateOnly, WeatherCondition>
            {
                { trip.Days[0].Date, WeatherCondition.Clear },
                { trip.Days[1].Date, WeatherCondition.Good },
                { trip.Days[2].Date, WeatherCondition.Clear }
            });

        // Act
        var result = await _handler.Handle(new RefreshWeather(tripId, "user-42"), CancellationToken.None);

        // Assert
        Assert.IsFalse(result.Updated);
        Assert.AreEqual(0, result.DaysRefreshed);
        Assert.AreEqual(0, result.Changes.Count);

        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new RefreshWeather(tripId, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WrongOwner_ThrowsTripForbiddenException()
    {
        var trip = CreateTrip("user-99"); // different owner
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<TripForbiddenException>(
            () => _handler.Handle(new RefreshWeather(tripId, "user-42"), CancellationToken.None));

        Assert.IsNotNull(exception);
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

        // Act
        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new RefreshWeather(tripId, "user-42"), CancellationToken.None));

        // Assert
        Assert.IsNotNull(exception);
        StringAssert.Contains(exception!.Message, "Itinerary not generated");
        _weatherProviderMock.Verify(
            w => w.GetWeatherAsync(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
