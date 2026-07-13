using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class DeleteTripHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly DeleteTripHandler _handler;

    public DeleteTripHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        _handler = new DeleteTripHandler(
            _tripRepoMock.Object,
            _userContextMock.Object,
            Mock.Of<ILogger<DeleteTripHandler>>());
    }

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip(string ownerUserId = "user-42")
    {
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

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

        return trip;
    }

    [TestMethod]
    public async Task Handle_OwnerMatches_DeletesTripAndReturnsUnit()
    {
        // Arrange
        var trip = CreateTrip("user-42");
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _tripRepoMock.Setup(r => r.DeleteAsync(tripId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new DeleteTrip(tripId), CancellationToken.None);

        // Assert
        Assert.AreEqual(Unit.Value, result);
        _tripRepoMock.Verify(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.DeleteAsync(tripId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_NonMatchingOwner_ThrowsTripForbiddenException()
    {
        // Arrange
        var trip = CreateTrip("user-99");
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // Act
        var exception = await CatchExceptionAsync<TripForbiddenException>(
            () => _handler.Handle(new DeleteTrip(tripId), CancellationToken.None));

        // Assert
        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        // Arrange
        var tripId = Guid.NewGuid();

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        // Act
        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new DeleteTrip(tripId), CancellationToken.None));

        // Assert
        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
}
