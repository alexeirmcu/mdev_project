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
public sealed class GenerateTripItineraryHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IItineraryGenerator> _itineraryGenMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<IOutboxWriter> _outboxWriterMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly GenerateTripItineraryHandler _handler;

    public GenerateTripItineraryHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        _handler = new GenerateTripItineraryHandler(
            _tripRepoMock.Object,
            _placeRepoMock.Object,
            _itineraryGenMock.Object,
            _weatherProviderMock.Object,
            _outboxWriterMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<GenerateTripItineraryHandler>>(),
            _userContextMock.Object);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None));

        Assert.IsNotNull(exception);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            It.IsAny<Trip>(),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            () => _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None));

        Assert.IsNotNull(exception);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            It.IsAny<Trip>(),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_TripWithoutBaseHotel_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip();
        // Set BaseHotel to null via reflection since the mock mapper won't set it
        var baseHotelProp = typeof(Trip).GetProperty("BaseHotel")!;
        baseHotelProp.SetValue(trip, null);

        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None));

        Assert.IsNotNull(exception);
        Assert.IsTrue(exception!.Message.Contains("BaseHotel"));
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            It.IsAny<Trip>(),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_CreatedTrip_GeneratesItinerary()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var candidatePlaces = new List<Place> { CreatePlaceEntity(100L) };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
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

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(m => m.Map<TripPlanResponse>(trip, It.IsAny<Action<IMappingOperationOptions>>()))
            .Returns(new TripPlanResponse(
                trip.TripId, trip.TripCode, trip.CityId, "madrid-es", "Madrid",
                trip.StartDate, trip.EndDate,
                new LocationModel("Hotel Central", 40.4168, -3.7038),
                new TravelersInput(2, 0, 0), new TripPreferencesInput(false, 30, true),
                new List<MustSeeResponse>(), "GENERATED", "09:00"));

        await _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None);

        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_GeneratedTrip_RegeneratesItinerary()
    {
        var trip = CreateTrip();
        trip.GenerateDays(); // creates skeleton days
        var tripId = trip.TripId;
        var initialDayCount = trip.Days.Count;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var candidatePlaces = new List<Place> { CreatePlaceEntity(100L) };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
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

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(m => m.Map<TripPlanResponse>(trip, It.IsAny<Action<IMappingOperationOptions>>()))
            .Returns(new TripPlanResponse(
                trip.TripId, trip.TripCode, trip.CityId, "madrid-es", "Madrid",
                trip.StartDate, trip.EndDate,
                new LocationModel("Hotel Central", 40.4168, -3.7038),
                new TravelersInput(2, 0, 0), new TripPreferencesInput(false, 30, true),
                new List<MustSeeResponse>(), "GENERATED", "09:00"));

        await _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None);

        // Days should still be valid count after regeneration (GenerateDays clears and rebuilds)
        Assert.AreEqual(initialDayCount, trip.Days.Count);
        Assert.IsTrue(trip.Days.Any());
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
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

    private static void SetEntityId<T>(T entity, long id) where T : class
    {
        var entityType = typeof(global::SmartTripPlanner.Domain.Base.Entity);
        var field = entityType.GetField("_Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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

    [TestMethod]
    public async Task Handle_WithUnenrichedPlaces_EnqueuesOutboxMessages()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;
        trip.GenerateDays();
        var place1 = CreatePlaceEntity(100L);
        var place2 = CreatePlaceEntity(101L);
        var place3 = CreatePlaceEntity(102L);
        typeof(Place).GetProperty("IsEnriched")!.SetValue(place3, true);

        // Add activities for place1 and place2 across days
        trip.Days[0].AddActivity(BlockType.Morning, new ActivityNode(place1.Id, "Activity 1", 0, 60));
        trip.Days[0].AddActivity(BlockType.Afternoon, new ActivityNode(place2.Id, "Activity 2", 0, 60));

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var candidatePlaces = new List<Place> { place1, place2, place3 };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
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

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(m => m.Map<TripPlanResponse>(trip, It.IsAny<Action<IMappingOperationOptions>>()))
            .Returns(new TripPlanResponse(
                trip.TripId, trip.TripCode, trip.CityId, "madrid-es", "Madrid",
                trip.StartDate, trip.EndDate,
                new LocationModel("Hotel Central", 40.4168, -3.7038),
                new TravelersInput(2, 0, 0), new TripPreferencesInput(false, 30, true),
                new List<MustSeeResponse>(), "GENERATED", "09:00"));

        await _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None);

        _outboxWriterMock.Verify(w => w.EnqueueAsync(
            It.Is<IEnumerable<string>>(refIds =>
                refIds.Count() == 2 &&
                refIds.Contains("fsq-100") &&
                refIds.Contains("fsq-101")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_AllPlacesEnriched_DoesNotEnqueueOutbox()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;
        trip.GenerateDays();
        var enrichedPlace1 = CreatePlaceEntity(100L);
        typeof(Place).GetProperty("IsEnriched")!.SetValue(enrichedPlace1, true);

        trip.Days[0].AddActivity(BlockType.Morning, new ActivityNode(enrichedPlace1.Id, "Activity 1", 0, 60));

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var candidatePlaces = new List<Place> { enrichedPlace1 };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
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

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(m => m.Map<TripPlanResponse>(trip, It.IsAny<Action<IMappingOperationOptions>>()))
            .Returns(new TripPlanResponse(
                trip.TripId, trip.TripCode, trip.CityId, "madrid-es", "Madrid",
                trip.StartDate, trip.EndDate,
                new LocationModel("Hotel Central", 40.4168, -3.7038),
                new TravelersInput(2, 0, 0), new TripPreferencesInput(false, 30, true),
                new List<MustSeeResponse>(), "GENERATED", "09:00"));

        await _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None);

        _outboxWriterMock.Verify(w => w.EnqueueAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_OutboxWriterThrows_StillCallsUpdateAsync()
    {
        var trip = CreateTrip();
        var tripId = trip.TripId;
        trip.GenerateDays();
        var unenrichedPlace = CreatePlaceEntity(100L);

        trip.Days[0].AddActivity(BlockType.Morning, new ActivityNode(unenrichedPlace.Id, "Activity 1", 0, 60));

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var candidatePlaces = new List<Place> { unenrichedPlace };
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                trip.CityId, It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
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

        _outboxWriterMock.Setup(w => w.EnqueueAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Outbox error"));

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(m => m.Map<TripPlanResponse>(It.IsAny<Trip>(), It.IsAny<Action<IMappingOperationOptions>>()))
            .Returns(new TripPlanResponse(
                trip.TripId, trip.TripCode, trip.CityId, "madrid-es", "Madrid",
                trip.StartDate, trip.EndDate,
                new LocationModel("Hotel Central", 40.4168, -3.7038),
                new TravelersInput(2, 0, 0), new TripPreferencesInput(false, 30, true),
                new List<MustSeeResponse>(), "GENERATED", "09:00"));

        try
        {
            await _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Handler threw unexpected exception: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        // UpdateAsync should still be called despite OutboxWriter failure
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }
}