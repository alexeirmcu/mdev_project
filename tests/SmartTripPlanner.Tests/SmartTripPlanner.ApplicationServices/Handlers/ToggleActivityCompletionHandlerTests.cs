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
public sealed class ToggleActivityCompletionHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly IMapper _mapper;
    private readonly ToggleActivityCompletionHandler _handler;

    public ToggleActivityCompletionHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _handler = new ToggleActivityCompletionHandler(
            _tripRepoMock.Object,
            Mock.Of<ILogger<ToggleActivityCompletionHandler>>(),
            _userContextMock.Object);
    }

    private static Trip CreateTripWithActivities(string ownerUserId = "user-42")
    {
        var city = new City("madrid-es", "Madrid", true);
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-TEST",
            CityId = 1L,
            City = city,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 3),
            BaseHotel = new Location("Hotel Central", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        trip.GenerateDaysFrom(trip.StartDate);

        // Add activities across blocks
        trip.Days[0].Morning.ForceAddActivity(
            new ActivityNode(100L, "Museum", 1, 120, true));
        trip.Days[0].Morning.ForceAddActivity(
            new ActivityNode(101L, "Park", 2, 60, false));

        trip.Days[0].Afternoon.ForceAddActivity(
            new ActivityNode(200L, "Restaurant", 1, 90, true));

        trip.Days[0].Evening.ForceAddActivity(
            new ActivityNode(300L, "Show", 1, 120, true));

        trip.Days[1].Morning.ForceAddActivity(
            new ActivityNode(400L, "Tour", 1, 180, false));

        return trip;
    }

    [TestMethod]
    public async Task Handle_ToggleCompleted_Success()
    {
        // Arrange
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(100L, true);

        // Act - toggle activity 100 (Museum, day 0, morning block) to completed
        var result = await _handler.Handle(
            new ToggleActivityCompletion(tripId, 0, request, "user-42"),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(100L, result.PlaceId);
        Assert.IsTrue(result.IsCompleted);
        Assert.AreEqual(1, result.CompletedCount);

        Assert.IsTrue(trip.Days[0].Morning.Activities[0].IsCompleted);

        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_ToggleUncompleted_Success()
    {
        // Arrange - mark activity 100 as completed first
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;
        trip.Days[0].Morning.Activities[0].SetCompleted(true);

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(100L, false);

        // Act
        var result = await _handler.Handle(
            new ToggleActivityCompletion(tripId, 0, request, "user-42"),
            CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(100L, result.PlaceId);
        Assert.IsFalse(result.IsCompleted);
        Assert.AreEqual(0, result.CompletedCount);

        Assert.IsFalse(trip.Days[0].Morning.Activities[0].IsCompleted);

        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_LocateAcrossMorningBlock_Success()
    {
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(101L, true);
        var result = await _handler.Handle(
            new ToggleActivityCompletion(tripId, 0, request, "user-42"),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(101L, result.PlaceId);
        Assert.IsTrue(trip.Days[0].Morning.Activities[1].IsCompleted);

        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_LocateAcrossAfternoonBlock_Success()
    {
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(200L, true);
        var result = await _handler.Handle(
            new ToggleActivityCompletion(tripId, 0, request, "user-42"),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(200L, result.PlaceId);
        Assert.IsTrue(trip.Days[0].Afternoon.Activities[0].IsCompleted);

        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_LocateAcrossEveningBlock_Success()
    {
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(300L, true);
        var result = await _handler.Handle(
            new ToggleActivityCompletion(tripId, 0, request, "user-42"),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(300L, result.PlaceId);
        Assert.IsTrue(trip.Days[0].Evening.Activities[0].IsCompleted);

        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_ActivityNotFound_ThrowsActivityNotFoundException()
    {
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(999L, true);
        var exception = await CatchExceptionAsync<ActivityNotFoundException>(
            () => _handler.Handle(
                new ToggleActivityCompletion(tripId, 0, request, "user-42"),
                CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_DayNotFound_ThrowsDayNotFoundException()
    {
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(100L, true);
        var exception = await CatchExceptionAsync<DayNotFoundException>(
            () => _handler.Handle(
                new ToggleActivityCompletion(tripId, 99, request, "user-42"),
                CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_FutureDayCompletion_ThrowsBusinessRuleException()
    {
        // Trip is in the future (next month). Today is June 24, so all days are in the future.
        var city = new City("madrid-es", "Madrid", true);
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-FUT",
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
        trip.GenerateDaysFrom(trip.StartDate);
        trip.Days[0].Morning.ForceAddActivity(
            new ActivityNode(100L, "Museum", 1, 120, true));
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(100L, true);

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(
                new ToggleActivityCompletion(tripId, 0, request, "user-42"),
                CancellationToken.None));

        Assert.IsNotNull(exception);
        StringAssert.Contains(exception!.Message, "Cannot complete an activity in a future day");
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

        var request = new ActivityCompletionRequest(100L, true);

        // Act
        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(
                new ToggleActivityCompletion(tripId, 0, request, "user-42"),
                CancellationToken.None));

        // Assert
        Assert.IsNotNull(exception);
        StringAssert.Contains(exception!.Message, "Itinerary not generated");
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var request = new ActivityCompletionRequest(100L, true);
        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(
                new ToggleActivityCompletion(tripId, 0, request, "user-42"),
                CancellationToken.None));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public async Task Handle_WrongOwner_ThrowsTripForbiddenException()
    {
        var trip = CreateTripWithActivities("user-99");
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(100L, true);
        var exception = await CatchExceptionAsync<TripForbiddenException>(
            () => _handler.Handle(
                new ToggleActivityCompletion(tripId, 0, request, "user-42"),
                CancellationToken.None));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public async Task Handle_CountsAllCompletedAcrossTrip()
    {
        var trip = CreateTripWithActivities();
        var tripId = trip.TripId;

        // Pre-set activity 200 and 300 as completed
        trip.Days[0].Afternoon.Activities[0].SetCompleted(true);
        trip.Days[0].Evening.Activities[0].SetCompleted(true);

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new ActivityCompletionRequest(100L, true);
        var result = await _handler.Handle(
            new ToggleActivityCompletion(tripId, 0, request, "user-42"),
            CancellationToken.None);

        // 3 completed: 100 (just toggled) + 200 + 300
        Assert.AreEqual(3, result.CompletedCount);
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
