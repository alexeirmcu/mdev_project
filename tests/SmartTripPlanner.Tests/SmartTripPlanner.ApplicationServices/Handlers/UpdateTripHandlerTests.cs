using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class UpdateTripHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly UpdateTripHandler _handler;

    public UpdateTripHandlerTests()
    {
        _handler = new UpdateTripHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _placeRepoMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<UpdateTripHandler>>());
    }

    private static Trip CreateTrip(TripStatus status = TripStatus.CREATED)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "MAD-2026-TEST",
            CityId = 1L,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 3),
            BaseHotel = new Location("Hotel Central", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Use reflection to set status since it has private set
        var statusField = typeof(Trip).GetProperty("Status", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        // Status has a private setter, so we set via the backing field approach
        if (status != TripStatus.CREATED)
        {
            var field = typeof(Trip).GetField("<Status>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(trip, status);
        }

        return trip;
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new UpdateTrip(tripId, new TripUpdateRequest()), CancellationToken.None));

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public async Task Handle_AddMustSee_WithValidPlaceId_Succeeds()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlaceEntity(42L) });

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.HasCount(1, trip.OriginalMustSees);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_GENERATEDStatus_BlocksModifyingDates()
    {
        var trip = CreateTrip(TripStatus.GENERATED);
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            StartDate: new DateOnly(2026, 8, 1)));

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(request, CancellationToken.None));

        Assert.IsNotNull(exception);
        Assert.IsTrue(exception!.Message.Contains("GENERATED"));
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_RemoveMustSee_Existing_Succeeds()
    {
        var trip = CreateTrip();
        trip.AddMustSee(new MustSee(42L, Priority.High));
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToRemove: new List<long> { 42L }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.IsEmpty(trip.OriginalMustSees);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_RemoveMustSee_NonExisting_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToRemove: new List<long> { 999L }));

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(request, CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void SetEntityId<T>(T entity, long id) where T : class
    {
        var entityType = typeof(global::SmartTripPlanner.Domain.Base.Entity);
        var field = entityType.GetField("_Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(entity, id);
    }

    private static Place CreatePlaceEntity(long id)
    {
        var place = new Place($"fsq-{id}", $"Place {id}", 1L, new PlaceLocation(0, 0));
        SetEntityId(place, id);
        return place;
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
