using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Services;
using Moq;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class TransitEnricherTests
{
    private readonly Mock<ITransitCalculator> _transitMock;
    private readonly TransitEnricher _enricher;

    public TransitEnricherTests()
    {
        _transitMock = new Mock<ITransitCalculator>();
        _transitMock
            .Setup(t => t.EstimateAsync(
                It.IsAny<PlaceLocation>(),
                It.IsAny<PlaceLocation>(),
                It.IsAny<TransportMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlaceLocation from, PlaceLocation to, TransportMode mode, CancellationToken _) =>
            {
                var distance = from.DistanceKmTo(to);
                var (speed, buffer) = mode switch
                {
                    TransportMode.WALK_AND_PUBLIC_TRANSPORT => (15.0, 10),
                    TransportMode.CAR => (30.0, 5),
                    _ => (15.0, 10)
                };
                var duration = Math.Max(2, (int)Math.Ceiling((distance / speed) * 60.0));
                return new TransitEstimate(duration, buffer, mode == TransportMode.WALK_AND_PUBLIC_TRANSPORT && distance > 2.0);
            });

        _enricher = new TransitEnricher(_transitMock.Object);
    }

    private static Place CreatePlace(long id, string name, double lat, double lng, int duration = 60)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng), duration, false, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));

        return place;
    }

    private static Trip CreateTrip(int dayCount = 1)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "TEST",
            CityId = 1,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 1 + dayCount - 1),
            BaseHotel = new Location("Hotel", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            CreatedAt = DateTimeOffset.UtcNow
        };

        trip.GenerateDays();
        return trip;
    }

    [TestMethod]
    public async Task EnrichAsync_SetsWeatherSummaryPerDay()
    {
        var trip = CreateTrip(dayCount: 2);
        var placesById = new Dictionary<long, Place>();

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear },
            { new DateOnly(2026, 7, 2), WeatherCondition.Bad }
        };

        await _enricher.EnrichAsync(trip, placesById, weather, CancellationToken.None);

        Assert.AreEqual(WeatherCondition.Clear, trip.Days[0].WeatherSummary);
        Assert.AreEqual(WeatherCondition.Bad, trip.Days[1].WeatherSummary);
    }

    [TestMethod]
    public async Task EnrichAsync_ConsecutiveActivities_GetsTransitAssigned()
    {
        var trip = CreateTrip(dayCount: 1);
        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(40.4154, -3.7074);

        var place1 = CreatePlace(1, "Place A", loc1.Latitude, loc1.Longitude);
        var place2 = CreatePlace(2, "Place B", loc2.Latitude, loc2.Longitude);
        var placesById = new Dictionary<long, Place>
        {
            { 1, place1 },
            { 2, place2 }
        };

        // Add consecutive activities to morning block
        var act1 = new ActivityNode(1, "Place A", 1, 60, location: loc1);
        var act2 = new ActivityNode(2, "Place B", 2, 60, location: loc2);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        await _enricher.EnrichAsync(trip, placesById, new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        // Transit should be assigned using ActivityNode.Location
        Assert.IsNotNull(act1.TransitToNext, "Transit should be assigned between consecutive activities");
    }

    [TestMethod]
    public async Task EnrichAsync_SingleActivity_NoTransitAssigned()
    {
        var trip = CreateTrip(dayCount: 1);
        var loc = new PlaceLocation(40.4168, -3.7038);
        var place = CreatePlace(1, "Single Place", loc.Latitude, loc.Longitude);
        var placesById = new Dictionary<long, Place> { { 1, place } };

        var act = new ActivityNode(1, "Single", 1, 60, location: loc);
        trip.Days[0].AddActivity(BlockType.Morning, act);

        await _enricher.EnrichAsync(trip, placesById, new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        // Single activity → no transit needed
        Assert.IsNull(act.TransitToNext);
    }

    [TestMethod]
    public async Task EnrichAsync_UsesActivityNodeLocation_NotPlacesByIdLookup()
    {
        var trip = CreateTrip(dayCount: 1);
        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(40.4154, -3.7074);

        // Add activities with locations directly
        var act1 = new ActivityNode(1, "Place A", 1, 60, location: loc1);
        var act2 = new ActivityNode(2, "Place B", 2, 60, location: loc2);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        // Pass empty placesById — enrichment should still work via ActivityNode.Location
        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        // Transit should be assigned using ActivityNode.Location (not placesById lookup)
        Assert.IsNotNull(act1.TransitToNext,
            "Transit should be assigned using ActivityNode.Location even with empty placesById");
    }

    [TestMethod]
    public async Task EnrichAsync_ActivityNodeLocationNull_SkipsTransit()
    {
        var trip = CreateTrip(dayCount: 1);

        // Create ActivityNode with null location
        var act1 = new ActivityNode(1, "Place A", 1, 60);
        var act2 = new ActivityNode(2, "Place B", 2, 60);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        // Both locations null → transit should be null
        Assert.IsNull(act1.TransitToNext);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Hotel transit tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichAsync_HotelTransit_PopulatedWhenBaseHotelPresent()
    {
        var trip = CreateTrip(dayCount: 1);
        var hotelLocation = new PlaceLocation(40.4168, -3.7038);
        var activityLocation = new PlaceLocation(40.4200, -3.7100);

        var act = new ActivityNode(1, "Place A", 1, 60, location: activityLocation);
        trip.Days[0].AddActivity(BlockType.Morning, act);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].Morning;
        Assert.IsNotNull(morning.TransitFromHotel, "TransitFromHotel should be computed when BaseHotel is set");
        Assert.IsNotNull(morning.TransitToHotel, "TransitToHotel should be computed when BaseHotel is set");
        Assert.IsTrue(morning.TransitFromHotel.DurationMinutes > 0);
        Assert.IsTrue(morning.TransitToHotel.DurationMinutes > 0);
    }

    [TestMethod]
    public async Task EnrichAsync_HotelTransit_NullWhenBaseHotelNull()
    {
        var trip = CreateTrip(dayCount: 1);
        trip.BaseHotel = null; // Remove hotel

        var act = new ActivityNode(1, "Place A", 1, 60, location: new PlaceLocation(40.4200, -3.7100));
        trip.Days[0].AddActivity(BlockType.Morning, act);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].Morning;
        Assert.IsNull(morning.TransitFromHotel);
        Assert.IsNull(morning.TransitToHotel);
    }

    [TestMethod]
    public async Task EnrichAsync_HotelTransit_NullForEmptyBlock()
    {
        var trip = CreateTrip(dayCount: 1);
        // No activities added to Morning block

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].Morning;
        Assert.IsNull(morning.TransitFromHotel);
        Assert.IsNull(morning.TransitToHotel);
    }

    [TestMethod]
    public async Task EnrichAsync_HotelTransit_RespectsTransportModeRules()
    {
        // Set car available and long distance from hotel to first activity
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(carAvailable: true, 30, true);

        // Hotel near Madrid center (40.4168, -3.7038), activity far away (Toledo ~65km)
        var activityLocation = new PlaceLocation(39.8628, -4.0273);
        var act = new ActivityNode(1, "Toledo", 1, 60, location: activityLocation);
        trip.Days[0].AddActivity(BlockType.Morning, act);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { new DateOnly(2026, 7, 1), WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].Morning;
        Assert.IsNotNull(morning.TransitFromHotel);

        // Long distance with car available → should be CAR or at least have a valid mode
        Assert.IsTrue(
            morning.TransitFromHotel.TransportMode == TransportMode.CAR,
            "Long distance from hotel with car available should use CAR");
    }
}
