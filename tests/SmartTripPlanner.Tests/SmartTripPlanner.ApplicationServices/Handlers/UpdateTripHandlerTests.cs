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
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IItineraryGenerator> _itineraryGenMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly UpdateTripHandler _handler;

    public UpdateTripHandlerTests()
    {
        _handler = new UpdateTripHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _placeRepoMock.Object,
            _itineraryGenMock.Object,
            _weatherProviderMock.Object,
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

    [TestMethod]
    public async Task Handle_AddMustSee_OnGeneratedTrip_RegeneratesItinerary()
    {
        var trip = CreateTrip(TripStatus.GENERATED);
        trip.GenerateDays();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlaceEntity(42L) });

        var candidatePlaces = new List<Place> { CreatePlaceEntity(100L) };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidatePlaces);

        var weatherData = new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 2), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 3), WeatherCondition.Clear }
        };
        _weatherProviderMock.Setup(w => w.GetWeatherAsync(
                trip.CityId, trip.StartDate, trip.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weatherData);

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(1, trip.OriginalMustSees.Count);
        Assert.AreEqual(3, trip.Days.Count);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_RemoveMustSee_OnGeneratedTrip_RegeneratesItinerary()
    {
        var trip = CreateTrip(TripStatus.GENERATED);
        trip.GenerateDays();
        trip.AddMustSee(new MustSee(42L, Priority.High));
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var candidatePlaces = new List<Place> { CreatePlaceEntity(100L) };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidatePlaces);

        var weatherData = new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 2), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 3), WeatherCondition.Clear }
        };
        _weatherProviderMock.Setup(w => w.GetWeatherAsync(
                trip.CityId, trip.StartDate, trip.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weatherData);

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToRemove: new List<long> { 42L }));

        await _handler.Handle(request, CancellationToken.None);

        Assert.IsEmpty(trip.OriginalMustSees);
        Assert.AreEqual(3, trip.Days.Count);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_AddMustSee_OnCreatedTrip_DoesNotRegenerate()
    {
        var trip = CreateTrip(TripStatus.CREATED);
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
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) },
            GenerateItinerary: false));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(1, trip.OriginalMustSees.Count);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            It.IsAny<Trip>(),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithGenerateItineraryFalse_DoesNotRegenerate()
    {
        var trip = CreateTrip(TripStatus.GENERATED);
        trip.GenerateDays();
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
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) },
            GenerateItinerary: false));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(1, trip.OriginalMustSees.Count);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            It.IsAny<Trip>(),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithGenerateItineraryTrueAndCreatedStatus_GeneratesItinerary()
    {
        var trip = CreateTrip(TripStatus.CREATED);
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { CreatePlaceEntity(42L) });

        var candidatePlaces = new List<Place> { CreatePlaceEntity(100L) };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidatePlaces);

        var weatherData = new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 2), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 3), WeatherCondition.Clear }
        };
        _weatherProviderMock.Setup(w => w.GetWeatherAsync(
                trip.CityId, trip.StartDate, trip.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weatherData);

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        var request = new UpdateTrip(tripId, new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput> { new(42L, Priority.High) },
            GenerateItinerary: true));

        await _handler.Handle(request, CancellationToken.None);

        Assert.AreEqual(1, trip.OriginalMustSees.Count);
        Assert.AreEqual(TripStatus.GENERATED, trip.Status);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
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
