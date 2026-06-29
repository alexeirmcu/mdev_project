using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
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
public sealed class UpdateTripHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly UpdateTripHandler _handler;

    public UpdateTripHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        // Default mapper setup for MustSee mapping used in AddMustSeeAsync
        _mapperMock
            .Setup(m => m.Map<MustSee>(It.IsAny<MustSeeInput>()))
            .Returns((MustSeeInput input) => new MustSee(input.PlaceId, string.Empty, input.Priority, input.PinnedDayIndex, input.PinnedBlock));

        _handler = new UpdateTripHandler(
            _tripRepoMock.Object,
            _placeRepoMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<UpdateTripHandler>>(),
            _userContextMock.Object);
    }

    private static Trip CreateTrip()
    {
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

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
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };

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
    public async Task Handle_NonMatchingOwner_ThrowsTripForbiddenException()
    {
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

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
            OwnerUserId = "user-99",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<TripForbiddenException>(
            () => _handler.Handle(new UpdateTrip(tripId, new TripUpdateRequest()), CancellationToken.None));

        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_UpdateOnStartedTrip_ThrowsBusinessRuleException()
    {
        // Trip with start date in the past
        var trip = CreateTrip();
        // Override StartDate to be yesterday
        var startDateField = typeof(Trip).GetProperty("StartDate")!;
        startDateField.SetValue(trip, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        var tripId = trip.TripId;
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            StartDate: new DateOnly(2026, 8, 1)));

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(request, CancellationToken.None));

        Assert.IsNotNull(exception);
        Assert.IsTrue(exception!.Message.Contains("already started"));
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(1, trip.OriginalMustSees.Count);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_RemoveMustSee_Existing_Succeeds()
    {
        var trip = CreateTrip();
        trip.AddMustSee(new MustSee(42L, "Museum", Priority.High));
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

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

    [TestMethod]
    public async Task Handle_UpdateOnlyDates_Succeeds()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 5)));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(new DateOnly(2026, 8, 1), trip.StartDate);
        Assert.AreEqual(new DateOnly(2026, 8, 5), trip.EndDate);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_UpdateOnlyHotel_Succeeds()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _mapperMock.Setup(m => m.Map<Location>(It.IsAny<LocationModel>()))
            .Returns(new Location("New Hotel", 41.0, 2.0));

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            BaseHotel: new LocationModel("New Hotel", 41.0, 2.0)));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual("New Hotel", trip.BaseHotel!.Name);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_UpdateOnlyTravelers_Succeeds()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _mapperMock.Setup(m => m.Map<Travelers>(It.IsAny<TravelersInput>()))
            .Returns(new Travelers(3, 1, 1));

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            Travelers: new TravelersInput(3, 1, 1)));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(3, trip.Travelers.Adults);
        Assert.AreEqual(1, trip.Travelers.Children);
        Assert.AreEqual(1, trip.Travelers.Infants);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_UpdateOnlyPreferences_Succeeds()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _mapperMock.Setup(m => m.Map<TripPreferences>(It.IsAny<TripPreferencesInput>()))
            .Returns(new TripPreferences(true, 60, false));

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            Preferences: new TripPreferencesInput(true, 60, false)));

        await _handler.Handle(request, CancellationToken.None);

        Assert.IsTrue(trip.Preferences.CarAvailable);
        Assert.AreEqual(60, trip.Preferences.MaxWalkingMinutes);
        Assert.IsFalse(trip.Preferences.WeatherAwareEnabled);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_UpdateOnlyMustSees_Succeeds()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlaceEntity(42L) });

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(1, trip.OriginalMustSees.Count);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_UpdateOnGeneratedTrip_ClearsDaysAndResetsStatus()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate); // creates skeleton days
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _mapperMock.Setup(m => m.Map<Location>(It.IsAny<LocationModel>()))
            .Returns(new Location("New Hotel", 41.0, 2.0));

        Assert.IsTrue(trip.Days.Count > 0);
        Assert.IsTrue(trip.Days.Any());

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            BaseHotel: new LocationModel("New Hotel", 41.0, 2.0)));

        await _handler.Handle(request, CancellationToken.None);

        Assert.IsFalse(trip.Days.Any());
        Assert.AreEqual(0, trip.Days.Count);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_UpdateMustSeeOnGeneratedTrip_ClearsDaysAndResetsStatus()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate);
        trip.AddMustSee(new MustSee(1L, "Museum", Priority.High));
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlaceEntity(42L) });

        Assert.IsTrue(trip.Days.Count > 0);
        Assert.IsTrue(trip.Days.Any());

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.IsFalse(trip.Days.Any());
        Assert.AreEqual(0, trip.Days.Count);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_EmptyUpdate_DoesNotChangeStatus()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate);
        var tripId = trip.TripId;
        var initialDayCount = trip.Days.Count;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // Empty update with no fields set
        var request = new UpdateTrip(tripId, new TripUpdateRequest());

        await _handler.Handle(request, CancellationToken.None);

        // Days should remain unchanged (no modification triggered)
        Assert.IsTrue(trip.Days.Any());
        Assert.AreEqual(initialDayCount, trip.Days.Count);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
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