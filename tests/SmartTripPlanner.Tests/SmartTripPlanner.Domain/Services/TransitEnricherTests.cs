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

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip(int dayCount = 1)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "TEST",
            CityId = 1,
            StartDate = FutureStartDate,
            EndDate = FutureStartDate.AddDays(dayCount - 1),
            BaseHotel = new Location("Hotel", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };

        trip.GenerateDaysFrom(trip.StartDate);
        return trip;
    }

    [TestMethod]
    public async Task EnrichAsync_SetsWeatherSummaryPerDay()
    {
        var trip = CreateTrip(dayCount: 2);
        var placesById = new Dictionary<long, Place>();

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear },
            { trip.StartDate.AddDays(1), WeatherCondition.Bad }
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
            { trip.StartDate, WeatherCondition.Clear }
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
            { trip.StartDate, WeatherCondition.Clear }
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
            { trip.StartDate, WeatherCondition.Clear }
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
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        // Both locations null → transit should be null
        Assert.IsNull(act1.TransitToNext);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MaxWalkingMinutes tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichAsync_WalkingBelowMaxWalkingMinutes_KeepsExistingBehavior()
    {
        // Walking estimate: (0.4/5)*60*0.3 = 1.44 min < default 30 → no guard trigger
        var trip = CreateTrip(dayCount: 1);

        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(40.4200, -3.7100); // ~0.4 km away

        var act1 = new ActivityNode(1, "Place A", 1, 60, location: loc1);
        var act2 = new ActivityNode(2, "Place B", 2, 60, location: loc2);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        // Walking below MaxWalkingMinutes → no alert, no CAR switch
        Assert.IsNotNull(act1.TransitToNext);
        Assert.AreEqual(TransportMode.WALK_AND_PUBLIC_TRANSPORT, act1.TransitToNext.TransportMode);
        Assert.IsFalse(act1.TransitToNext.FrictionAlert, "No friction alert when walking is within limit");
    }

    [TestMethod]
    public async Task EnrichAsync_WalkingExceedsMaxWithCarAvailable_SwitchesToCar()
    {
        // ~5 km distance: walking = (5/5)*60*0.3 = 18 min
        // Set MaxWalkingMinutes = 15 so guard triggers
        // CarAvailable = true so mode switches to CAR
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(carAvailable: true, maxWalkingMinutes: 15);

        // ~5 km north of hotel (40.4168, -3.7038)
        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(40.4618, -3.7038); // ~5 km north

        var act1 = new ActivityNode(1, "Place A", 1, 60, location: loc1);
        var act2 = new ActivityNode(2, "Place B", 2, 60, location: loc2);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        Assert.IsNotNull(act1.TransitToNext);
        Assert.AreEqual(TransportMode.CAR, act1.TransitToNext.TransportMode,
            "Should switch to CAR when walking exceeds MaxWalkingMinutes and car is available");
    }

    [TestMethod]
    public async Task EnrichAsync_WalkingExceedsMaxWithoutCar_SetsFrictionAlert()
    {
        // ~5 km distance: walking = (5/5)*60*0.3 = 18 min
        // Set MaxWalkingMinutes = 15 so guard triggers
        // CarAvailable = false → keep WALK_AND_PUBLIC_TRANSPORT with frictionAlert = true
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(carAvailable: false, maxWalkingMinutes: 15);

        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(40.4618, -3.7038); // ~5 km north

        var act1 = new ActivityNode(1, "Place A", 1, 60, location: loc1);
        var act2 = new ActivityNode(2, "Place B", 2, 60, location: loc2);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        Assert.IsNotNull(act1.TransitToNext);
        Assert.AreEqual(TransportMode.WALK_AND_PUBLIC_TRANSPORT, act1.TransitToNext.TransportMode,
            "Should keep WALK_AND_PUBLIC_TRANSPORT when no car available");
        Assert.IsTrue(act1.TransitToNext.FrictionAlert,
            "Should set FrictionAlert when walking exceeds MaxWalkingMinutes and no car");
    }

    [TestMethod]
    public async Task EnrichAsync_WalkingBelowDefaultMaxWalking_UsesExistingBehavior()
    {
        // Default MaxWalkingMinutes = 30
        var trip = CreateTrip(dayCount: 1);

        // ~5 km: walking = (5/5)*60*0.3 = 18 min < 30 → no guard trigger
        var loc1 = new PlaceLocation(40.4168, -3.7038);
        var loc2 = new PlaceLocation(40.4618, -3.7038); // ~5 km north

        var act1 = new ActivityNode(1, "Place A", 1, 60, location: loc1);
        var act2 = new ActivityNode(2, "Place B", 2, 60, location: loc2);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Morning, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        Assert.IsNotNull(act1.TransitToNext);
        // Default MaxWalkingMinutes (30) > walking estimate (18) → no behavioral change
        Assert.AreEqual(TransportMode.WALK_AND_PUBLIC_TRANSPORT, act1.TransitToNext.TransportMode);
        // FrictionAlert depends on mock: distance > 2.0 → true for WALK_AND_PUBLIC_TRANSPORT
        // This is the mock's existing behavior, not affected by our change
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
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
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
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
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
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.IsNull(morning.TransitFromHotel);
        Assert.IsNull(morning.TransitToHotel);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ReturnToHotelStrategy tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task EnrichAsync_AlwaysStrategy_HotelLegsPresentNoInterBlockTransit()
    {
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Always);

        var morningLoc = new PlaceLocation(40.4168, -3.7038);
        var afternoonLoc = new PlaceLocation(40.4200, -3.7100);

        var act1 = new ActivityNode(1, "Morning Place", 1, 60, location: morningLoc);
        var act2 = new ActivityNode(2, "Afternoon Place", 2, 60, location: afternoonLoc);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Afternoon, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // Always: each block independent — hotel legs present, no InterBlockTransit
        Assert.IsNotNull(morning.TransitFromHotel);
        Assert.IsNotNull(morning.TransitToHotel);
        Assert.IsNull(morning.InterBlockTransit);
        Assert.IsNotNull(afternoon.TransitFromHotel);
        Assert.IsNotNull(afternoon.TransitToHotel);
        Assert.IsNull(afternoon.InterBlockTransit);
    }

    [TestMethod]
    public async Task EnrichAsync_NeverStrategy_InterBlockTransitPresentHotelLegsNullAtBoundaries()
    {
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);

        var morningLoc = new PlaceLocation(40.4168, -3.7038);
        var afternoonLoc = new PlaceLocation(40.4200, -3.7100);

        var act1 = new ActivityNode(1, "Morning Place", 1, 60, location: morningLoc);
        var act2 = new ActivityNode(2, "Afternoon Place", 2, 60, location: afternoonLoc);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Afternoon, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // Never: Morning→Afternoon boundary — InterBlockTransit on destination (Afternoon), hotel legs null
        Assert.IsNotNull(morning.TransitFromHotel, "Morning should still have TransitFromHotel (start of day)");
        Assert.IsNull(morning.TransitToHotel, "Morning TransitToHotel should be null (inter-block transit used)");
        Assert.IsNull(morning.InterBlockTransit, "Morning InterBlockTransit is null — transit stored on destination block");

        Assert.IsNull(afternoon.TransitFromHotel, "Afternoon TransitFromHotel should be null (inter-block transit)");
        Assert.IsNotNull(afternoon.TransitToHotel, "Afternoon TransitToHotel should be present (end of block returns to hotel)");
        Assert.IsNotNull(afternoon.InterBlockTransit, "Afternoon should have InterBlockTransit from Morning (stored on destination)");
    }

    [TestMethod]
    public async Task EnrichAsync_NeverStrategy_EveningAlwaysReturnsToHotel()
    {
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);

        var afternoonLoc = new PlaceLocation(40.4200, -3.7100);
        var eveningLoc = new PlaceLocation(40.4180, -3.7140);

        var act1 = new ActivityNode(1, "Afternoon Place", 1, 60, location: afternoonLoc);
        var act2 = new ActivityNode(2, "Evening Place", 2, 60, location: eveningLoc);
        trip.Days[0].AddActivity(BlockType.Afternoon, act1);
        trip.Days[0].AddActivity(BlockType.Evening, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);
        var evening = trip.Days[0].GetBlock(BlockType.Evening);

        // Even with Never: Afternoon→Evening boundary optimized but Evening still returns to hotel
        Assert.IsNull(afternoon.TransitToHotel, "Afternoon TransitToHotel null — inter-block to Evening");
        Assert.IsNull(afternoon.InterBlockTransit, "Afternoon InterBlockTransit null — transit stored on destination (Evening)");
        Assert.IsNull(evening.TransitFromHotel, "Evening TransitFromHotel null — came via inter-block from Afternoon");
        Assert.IsNotNull(evening.InterBlockTransit, "Evening should have InterBlockTransit from Afternoon (stored on destination)");
        Assert.IsNotNull(evening.TransitToHotel, "Evening TransitToHotel MUST be present — end of day always returns to hotel");
    }

    [TestMethod]
    public async Task EnrichAsync_NeverStrategy_EmptyBlockBoundary_KeepsHotelTransit()
    {
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);

        // Only Morning has activities — Afternoon is empty
        var morningLoc = new PlaceLocation(40.4168, -3.7038);
        var act1 = new ActivityNode(1, "Morning Only", 1, 60, location: morningLoc);
        trip.Days[0].AddActivity(BlockType.Morning, act1);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);

        // With empty Afternoon, Morning should still have full hotel transit
        Assert.IsNotNull(morning.TransitFromHotel);
        Assert.IsNotNull(morning.TransitToHotel);
        Assert.IsNull(morning.InterBlockTransit);
    }

    [TestMethod]
    public async Task EnrichAsync_ProximityBased_ChoosesDirectWhenShorter()
    {
        // Setup: hotel is far from boundary locations, but locations are close to each other
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.ProximityBased);

        // Hotel at (40.4168, -3.7038) — Madrid center
        // Morning and Afternoon activities very close to each other but far from hotel
        // This should make direct transit shorter than via hotel
        var morningLoc = new PlaceLocation(40.5000, -3.8000);  // ~10km from hotel
        var afternoonLoc = new PlaceLocation(40.5010, -3.8010); // ~0.1km from morning

        var act1 = new ActivityNode(1, "Morning Place", 1, 60, location: morningLoc);
        var act2 = new ActivityNode(2, "Afternoon Place", 2, 60, location: afternoonLoc);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Afternoon, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // Direct should be chosen (very short distance between activities)
        // InterBlockTransit is on the destination block (Afternoon)
        Assert.IsNull(morning.TransitToHotel, "Direct route chosen — TransitToHotel should be null");
        Assert.IsNull(morning.InterBlockTransit, "Morning has no InterBlockTransit — stored on destination");
        Assert.IsNull(afternoon.TransitFromHotel, "Direct route — TransitFromHotel should be null");
        Assert.IsNotNull(afternoon.InterBlockTransit, "Afternoon should have InterBlockTransit from Morning (direct route)");
        Assert.IsNotNull(afternoon.TransitToHotel, "Afternoon still returns to hotel");
    }

    [TestMethod]
    public async Task EnrichAsync_ProximityBased_MechanismRunsAndMakesChoice()
    {
        // Verify ProximityBased executes, makes a choice, and doesn't crash
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(carAvailable: true, returnToHotelStrategy: ReturnToHotelStrategy.ProximityBased);

        // Activities close together (short direct transit) but far from hotel
        // With CarAvailable, the mock may use different modes — the key is that
        // the proximity mechanism computes both and makes a choice
        var morningLoc = new PlaceLocation(40.5000, -3.8000);  // ~10km from hotel
        var afternoonLoc = new PlaceLocation(40.5010, -3.8010); // ~0.1km from morning

        var act1 = new ActivityNode(1, "Morning Place", 1, 60, location: morningLoc);
        var act2 = new ActivityNode(2, "Afternoon Place", 2, 60, location: afternoonLoc);
        trip.Days[0].AddActivity(BlockType.Morning, act1);
        trip.Days[0].AddActivity(BlockType.Afternoon, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // The mechanism made a choice: either InterBlockTransit on destination (direct chosen)
        // OR hotel transit is kept (hotel chosen). One must be true.
        Assert.IsTrue(
            afternoon.InterBlockTransit != null || morning.TransitToHotel != null,
            "ProximityBased must set either InterBlockTransit (on destination) or keep TransitToHotel");
    }

    [TestMethod]
    public async Task EnrichAsync_ProximityBased_EveningAlwaysReturnsToHotel()
    {
        var trip = CreateTrip(dayCount: 1);
        trip.Preferences = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.ProximityBased);

        var afternoonLoc = new PlaceLocation(40.4200, -3.7100);
        var eveningLoc = new PlaceLocation(40.4180, -3.7140);

        var act1 = new ActivityNode(1, "Afternoon Place", 1, 60, location: afternoonLoc);
        var act2 = new ActivityNode(2, "Evening Place", 2, 60, location: eveningLoc);
        trip.Days[0].AddActivity(BlockType.Afternoon, act1);
        trip.Days[0].AddActivity(BlockType.Evening, act2);

        await _enricher.EnrichAsync(trip, new Dictionary<long, Place>(), new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var evening = trip.Days[0].GetBlock(BlockType.Evening);

        // Evening always returns to hotel regardless of strategy
        Assert.IsNotNull(evening.TransitToHotel, "Evening always returns to hotel");
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
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.IsNotNull(morning.TransitFromHotel);

        // Long distance with car available → should be CAR or at least have a valid mode
        Assert.IsTrue(
            morning.TransitFromHotel.TransportMode == TransportMode.CAR,
            "Long distance from hotel with car available should use CAR");
    }
}
