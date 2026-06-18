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
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<IItineraryGenerator> _itineraryGenMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly GenerateTripItineraryHandler _handler;

    public GenerateTripItineraryHandlerTests()
    {
        _handler = new GenerateTripItineraryHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _placeRepoMock.Object,
            _itineraryGenMock.Object,
            _weatherProviderMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<GenerateTripItineraryHandler>>());
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
    public async Task Handle_CompletedTrip_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip(TripStatus.COMPLETED);
        var tripId = trip.TripId;

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        var exception = await CatchExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None));

        Assert.IsNotNull(exception);
        Assert.IsTrue(exception!.Message.Contains("completed"));
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            It.IsAny<Trip>(),
            It.IsAny<IReadOnlyList<Place>>(),
            It.IsAny<Dictionary<DateOnly, WeatherCondition>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_CreatedTrip_GeneratesItinerary()
    {
        var trip = CreateTrip(TripStatus.CREATED);
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

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

        _mapperMock.Setup(m => m.Map<TripPlanResponse>(trip, It.IsAny<Action<IMappingOperationOptions>>()))
            .Returns(new TripPlanResponse(
                trip.TripId, trip.TripCode, trip.CityId, "madrid-es", "Madrid",
                trip.StartDate, trip.EndDate,
                new LocationModel("Hotel Central", 40.4168, -3.7038),
                new TravelersInput(2, 0, 0), new TripPreferencesInput(false, 30, true),
                new List<MustSeeResponse>(), "GENERATED", "09:00"));

        await _handler.Handle(new GenerateTripItinerary(tripId), CancellationToken.None);

        Assert.AreEqual(TripStatus.GENERATED, trip.Status);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_GeneratedTrip_RegeneratesItinerary()
    {
        var trip = CreateTrip(TripStatus.GENERATED);
        trip.GenerateDays(); // creates skeleton days
        var tripId = trip.TripId;
        var initialDayCount = trip.Days.Count;

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

        _tripRepoMock.Setup(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);
        _cityRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);

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
        Assert.AreEqual(TripStatus.GENERATED, trip.Status);
        _itineraryGenMock.Verify(g => g.GenerateAsync(
            trip, candidatePlaces, weatherData, It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(trip, It.IsAny<CancellationToken>()), Times.Once);
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

        if (status != TripStatus.CREATED)
        {
            var field = typeof(Trip).GetField("<Status>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(trip, status);
        }

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
}
