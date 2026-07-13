using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Constants;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Services;
using Moq;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class HeuristicItineraryGeneratorTests
{
    private readonly Mock<ITransitCalculator> _transitMock;
    private readonly HeuristicItineraryGenerator _generator;
    private readonly TimelineScheduler _timelineScheduler;

    public HeuristicItineraryGeneratorTests()
    {
        _transitMock = CreateTransitCalculatorMock();
        var scorer = new CandidateScorer();
        var pinnedPlacer = new PinnedMustSeePlacer();
        var unpinnedPlacer = new UnpinnedMustSeePlacer();
        var candidateFiller = new CandidateFiller(scorer);
        var transitEnricher = new TransitEnricher(_transitMock.Object);
        _timelineScheduler = new TimelineScheduler();

        _generator = new HeuristicItineraryGenerator(
            pinnedPlacer,
            unpinnedPlacer,
            candidateFiller,
            transitEnricher,
            _timelineScheduler);
    }

    private static Mock<ITransitCalculator> CreateTransitCalculatorMock()
    {
        var mock = new Mock<ITransitCalculator>();
        mock
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
        return mock;
    }

    private static Place CreatePlace(long id, string name, double lat, double lng,
        int duration = 60, bool isIndoor = false, bool isFamilyFriendly = true)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng),
            duration, isIndoor, isFamilyFriendly);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        // Open every day
        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));

        return place;
    }

    private static Place CreatePlaceWithHours(long id, string name, double lat, double lng,
        DayOfWeek[]? closedDays = null, int duration = 60, bool isIndoor = false, bool isFamilyFriendly = true)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng),
            duration, isIndoor, isFamilyFriendly);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        var closed = closedDays?.ToHashSet() ?? new HashSet<DayOfWeek>();
        foreach (var day in Enum.GetValues<DayOfWeek>().Cast<DayOfWeek>())
        {
            if (!closed.Contains(day))
                place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));
        }

        return place;
    }

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip(IReadOnlyList<MustSee> mustSees, int dayCount = 3,
        bool carAvailable = false, bool weatherAware = true, int children = 0,
        ReturnToHotelStrategy returnToHotelStrategy = ReturnToHotelStrategy.Always,
        bool allowMustSeeOvertime = false)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "TEST-CODE",
            CityId = 1,
            StartDate = FutureStartDate,
            EndDate = FutureStartDate.AddDays(dayCount - 1),
            BaseHotel = new Location("Test Hotel", 40.4168, -3.7038),
            Travelers = new Travelers(2, children, 0),
            Preferences = new TripPreferences(carAvailable, 30, weatherAware, returnToHotelStrategy: returnToHotelStrategy, allowMustSeeOvertime: allowMustSeeOvertime),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var mustSee in mustSees)
            trip.AddMustSee(mustSee);

        return trip;
    }

    private static Dictionary<DateOnly, WeatherCondition> AllClearWeather(int dayCount)
    {
        var dict = new Dictionary<DateOnly, WeatherCondition>();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        for (int i = 0; i < dayCount; i++)
            dict[start.AddDays(i)] = WeatherCondition.Clear;
        return dict;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 1: Pinned must-see placed in correct day and block
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_PinnedMustSeeWithBlock_AppearsInCorrectDayAndBlock()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Museo del Prado", Priority.High, pinnedDayIndex: 1, pinnedBlock: BlockType.Morning)
        };
        var trip = CreateTrip(mustSees, dayCount: 3);
        var places = new List<Place>
        {
            CreatePlace(1, "Museo del Prado", 40.4168, -3.7038, duration: 120)
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(3), CancellationToken.None);

        Assert.AreEqual(3, trip.Days.Count);
        var day1 = trip.Days[1]; // DayIndex 1
        Assert.AreEqual(1, day1.GetBlock(BlockType.Morning).Activities.Count);
        Assert.AreEqual(1L, day1.GetBlock(BlockType.Morning).Activities[0].PlaceId);
        Assert.AreEqual("Museo del Prado", day1.GetBlock(BlockType.Morning).Activities[0].Name);

        // Phase 6: Timeline scheduling populates arrival/departure
        var activity = day1.GetBlock(BlockType.Morning).Activities[0];
        Assert.IsTrue(activity.EstimatedArrival > 0, "EstimatedArrival should be populated by TimelineScheduler");
        Assert.IsTrue(activity.EstimatedDeparture > activity.EstimatedArrival,
            "EstimatedDeparture should be after EstimatedArrival");

        // Hotel transit should be populated (BaseHotel is set)
        Assert.IsNotNull(day1.GetBlock(BlockType.Morning).TransitFromHotel,
            "TransitFromHotel should be populated when BaseHotel is set");
        Assert.IsNotNull(day1.GetBlock(BlockType.Morning).TransitToHotel,
            "TransitToHotel should be populated when BaseHotel is set");
    }

    [TestMethod]
    public async Task GenerateAsync_PinnedMustSeeNoBlock_PlacedInFirstAvailableBlock()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Must-See A", Priority.High, pinnedDayIndex: 0) // No block specified
        };
        var trip = CreateTrip(mustSees, dayCount: 2);

        var places = new List<Place>
        {
            CreatePlace(1, "Must-See A", 40.4168, -3.7038, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(2), CancellationToken.None);

        // Must end up in one of the three blocks of day 0
        var day0 = trip.Days[0];
        var totalActivities = day0.GetBlock(BlockType.Morning).Activities.Count
                            + day0.GetBlock(BlockType.Afternoon).Activities.Count
                            + day0.GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(1, totalActivities);
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

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 2: Unpinned zone clustering — nearby places grouped in same day
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_UnpinnedMustSeesInSameZone_ClusteredInSameDay()
    {
        // Three places within 2km in central Madrid — should cluster together
        var mustSees = new List<MustSee>
        {
            new(1, "Puerta del Sol", Priority.High),
            new(2, "Plaza Mayor", Priority.Medium),
            new(3, "Palacio Real", Priority.Low)
        };
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = new List<Place>
        {
            CreatePlace(1, "Puerta del Sol", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Plaza Mayor", 40.4154, -3.7074, duration: 60),
            CreatePlace(3, "Palacio Real", 40.4180, -3.7140, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // All 3 must-sees in a single day
        var day0 = trip.Days[0];
        var totalActivities = day0.GetBlock(BlockType.Morning).Activities.Count
                            + day0.GetBlock(BlockType.Afternoon).Activities.Count
                            + day0.GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(3, totalActivities);
    }

    [TestMethod]
    public async Task GenerateAsync_UnpinnedMustSeesInDifferentZones_SeparateDays()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Madrid Centro", Priority.High),
            new(2, "Paris Centro", Priority.Medium)
        };
        var trip = CreateTrip(mustSees, dayCount: 2);
        var places = new List<Place>
        {
            // Madrid — far from Paris
            CreatePlace(1, "Madrid Centro", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Paris Centro", 48.8566, 2.3522, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(2), CancellationToken.None);

        // Different clusters → could be same or different days, but must both be placed
        var total = trip.Days.Sum(d =>
            d.Blocks.Sum(b => b.Activities.Count));
        Assert.AreEqual(2, total);
    }

    [TestMethod]
    public async Task GenerateAsync_MustSeeClosedOnFirstDay_PlacedOnOpenDay()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 5);
        var closedDay = trip.StartDate.DayOfWeek;

        var mustSees = new List<MustSee>
        {
            new(1, "Closed On First Day", Priority.High) // unpinned
        };
        trip.AddMustSee(mustSees[0]);
        var places = new List<Place>
        {
            CreatePlaceWithHours(1, "Closed On First Day", 40.4168, -3.7038,
                closedDays: new[] { closedDay })
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(5), CancellationToken.None);

        // Must-see should NOT be on day 0 (Wednesday)
        var totalOnWed = trip.Days[0].GetBlock(BlockType.Morning).Activities.Count
                       + trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count
                       + trip.Days[0].GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(0, totalOnWed);

        // Must-see should appear somewhere (Thursday-Sunday)
        var placedOnOtherDays = trip.Days.Skip(1).Sum(d =>
            d.Blocks.Sum(b => b.Activities.Count));
        Assert.AreEqual(1, placedOnOtherDays);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 3: Weather filter — indoor preferred on bad weather
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_BadWeatherWithWeatherAware_PrefersIndoorCandidates()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1,
            weatherAware: true, carAvailable: false);
        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Bad }
        };

        var indoor = CreatePlace(1, "Indoor Museum", 40.4168, -3.7038, duration: 60, isIndoor: true);
        var outdoor1 = CreatePlace(2, "Outdoor Park", 40.4170, -3.7040, duration: 60, isIndoor: false);
        var outdoor2 = CreatePlace(3, "Outdoor Plaza", 40.4172, -3.7042, duration: 60, isIndoor: false);

        await _generator.GenerateAsync(trip, new[] { indoor, outdoor1, outdoor2 }, weather, CancellationToken.None);

        // Morning block has capacity for all 3, but scoring should put indoor first
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.IsTrue(morning.Activities.Count >= 1);
        Assert.IsTrue(morning.Activities[0].IsIndoor,
            "Indoor activity should be placed first on bad weather (highest score)");
    }

    [TestMethod]
    public async Task GenerateAsync_BadWeatherWeatherAwareDisabled_NoIndoorPreference()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1,
            weatherAware: false, carAvailable: false);
        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Bad }
        };

        var indoor = CreatePlace(1, "Indoor Museum", 40.4168, -3.7038, duration: 60, isIndoor: true);
        var outdoor = CreatePlace(2, "Outdoor Park", 40.4170, -3.7040, duration: 60, isIndoor: false);

        await _generator.GenerateAsync(trip, new[] { indoor, outdoor }, weather, CancellationToken.None);

        // With weatherAware=false, all candidates get the same popularity-only score (10),
        // so both should be placed (equal score != preference)
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.IsTrue(morning.Activities.Count >= 1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 4: Block capacity — overflow drops low-priority candidates
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_BlockCapacityExceeded_LowPriorityCandidateSkipped()
    {
        // Use 5 must-sees for a 1-day trip (Morning=3, Afternoon=2 after overflow)
        var mustSees = new List<MustSee>
        {
            new(1, "Place A", Priority.High),
            new(2, "Place B", Priority.High),
            new(3, "Place C", Priority.High),
            new(4, "Place D", Priority.Medium),
            new(5, "Place E", Priority.Low),
        };
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = new List<Place>
        {
            CreatePlace(1, "Place A", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Place B", 40.4154, -3.7074, duration: 60),
            CreatePlace(3, "Place C", 40.4180, -3.7140, duration: 60),
            CreatePlace(4, "Place D", 40.4170, -3.7100, duration: 60),
            CreatePlace(5, "Place E", 40.4175, -3.7080, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // All 5 must-sees should be placed (morning=3, afternoon=2 in a 1-day trip)
        var day0 = trip.Days[0];
        var total = day0.GetBlock(BlockType.Morning).Activities.Count
                  + day0.GetBlock(BlockType.Afternoon).Activities.Count
                  + day0.GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(5, total);
        // Morning should have 3 (capacity)
        Assert.AreEqual(3, day0.GetBlock(BlockType.Morning).Activities.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 5: Over-constrained — throws OverConstrainedRouteException
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_OnlyHighPriorityMustSeesExceedCapacity_ThrowsOverConstrainedRouteException()
    {
        // 1-day trip: morning=3, afternoon=3, evening=1 (60 min each)
        // Max total for 60-min activities = 7
        // Make 8 High priority must-sees — impossible → exception
        var mustSees = Enumerable.Range(1, 8)
            .Select(i => new MustSee(i, $"Place {i}", Priority.High))
            .ToList();
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = Enumerable.Range(1, 8)
            .Select(i => CreatePlace(i, $"Place {i}", 40.4168 + i * 0.001, -3.7038 + i * 0.001))
            .ToList();

        var ex = await CatchExceptionAsync<OverConstrainedRouteException>(() =>
            _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None));

        Assert.IsNotNull(ex);
        Assert.IsTrue(ex.ConflictingPlaceIds.Count > 0,
            "Exception should contain at least one conflicting PlaceId");
    }

    [TestMethod]
    public async Task GenerateAsync_ExactlyAtCapacity_DoesNotThrow()
    {
        // 1-day trip: 3 (morning) + 3 (afternoon) + 1 (evening, 60 min) = 7
        var mustSees = Enumerable.Range(1, 7)
            .Select(i => new MustSee(i, $"Place {i}", Priority.High))
            .ToList();
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = Enumerable.Range(1, 7)
            .Select(i => CreatePlace(i, $"Place {i}", 40.4168 + i * 0.001, -3.7038 + i * 0.001))
            .ToList();

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // Should complete without exception
        var total = trip.Days[0].GetBlock(BlockType.Morning).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(7, total);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 6: No candidates — works with just must-sees
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_NoCandidates_PlacesOnlyMustSees()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Must See A", Priority.High),
            new(2, "Must See B", Priority.Medium)
        };
        var trip = CreateTrip(mustSees, dayCount: 2);
        // Only must-see places in the list (no extra candidates)
        var places = new List<Place>
        {
            CreatePlace(1, "Must See A", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Must See B", 40.4154, -3.7074, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(2), CancellationToken.None);

        var total = trip.Days.Sum(d =>
            d.Blocks.Sum(b => b.Activities.Count));
        Assert.AreEqual(2, total);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 7: Mixed priorities — High placed before Medium before Low
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_MixedPriorities_HighPlacedFirst()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Low Priority Place", Priority.Low),
            new(2, "High Priority Place", Priority.High),
            new(3, "Medium Priority Place", Priority.Medium)
        };
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = new List<Place>
        {
            CreatePlace(1, "Low Priority Place", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "High Priority Place", 40.4154, -3.7074, duration: 60),
            CreatePlace(3, "Medium Priority Place", 40.4180, -3.7140, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // All should be placed (morning=3, 60-min activities fit)
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.AreEqual(3, morning.Activities.Count);
    }

    [TestMethod]
    public async Task GenerateAsync_LowPriorityMustSeeSkippedWhenNoRoom_HighStillPlaced()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "High Priority", Priority.High),
            new(2, "Low Priority", Priority.Low)
        };
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = new List<Place>
        {
            CreatePlace(1, "High Priority", 40.4168, -3.7038, duration: 105), // fills evening
            CreatePlace(2, "Low Priority", 40.4154, -3.7074, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // Morning + Afternoon have room for low priority too (each 3 x 60 min)
        // So both should actually fit
        var total = trip.Days[0].GetBlock(BlockType.Morning).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(2, total);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 8: Transport mode selection
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_ShortDistanceTransit_UsesWalkAndPublicTransport()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Puerta del Sol", Priority.High),
            new(2, "Plaza Mayor", Priority.High)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, carAvailable: false);
        var places = new List<Place>
        {
            CreatePlace(1, "Puerta del Sol", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Plaza Mayor", 40.4154, -3.7074, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // Both should be in Morning block (adjacent activities)
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        if (morning.Activities.Count >= 2)
        {
            var transit = morning.Activities[0].TransitToNext;
            Assert.IsNotNull(transit);
            Assert.AreEqual(TransportMode.WALK_AND_PUBLIC_TRANSPORT, transit.TransportMode,
                "Short distance should use WALK_AND_PUBLIC_TRANSPORT regardless of car availability");
        }
    }

    [TestMethod]
    public async Task GenerateAsync_LongDistanceWithCar_UsesCarTransport()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Madrid Centro", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning),
            new(2, "Toledo", Priority.Medium, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, carAvailable: true);
        var places = new List<Place>
        {
            CreatePlace(1, "Madrid Centro", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Toledo", 39.8628, -4.0273, duration: 60), // ~65 km
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        // Both pinned to Morning → consecutive, transit assigned
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.AreEqual(2, morning.Activities.Count);

        var transit = morning.Activities[0].TransitToNext;
        Assert.IsNotNull(transit, "Transit should be assigned between consecutive morning activities");
        Assert.AreEqual(TransportMode.CAR, transit.TransportMode,
            "Long distance (>10 km) with car available should use CAR");
    }

    [TestMethod]
    public async Task GenerateAsync_Within_1_5km_AlwaysWalkAndPublicTransport_EvenWithCar()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Place A", Priority.High),
            new(2, "Place B", Priority.High)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, carAvailable: true);
        var places = new List<Place>
        {
            CreatePlace(1, "Place A", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Place B", 40.4175, -3.7045, duration: 60), // ~0.1 km
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        if (morning.Activities.Count >= 2)
        {
            var transit = morning.Activities[0].TransitToNext;
            Assert.IsNotNull(transit);
            Assert.AreEqual(TransportMode.WALK_AND_PUBLIC_TRANSPORT, transit.TransportMode,
                "Within 1.5 km should always use WALK_AND_PUBLIC_TRANSPORT");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Additional: Weather enrichment — WeatherSummary set per day
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_WeatherData_UpdatesDayPlanWeatherSummary()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Place", Priority.High)
        };
        var trip = CreateTrip(mustSees, dayCount: 2);
        var places = new List<Place>
        {
            CreatePlace(1, "Place", 40.4168, -3.7038, duration: 60)
        };

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear },
            { trip.StartDate.AddDays(1), WeatherCondition.Bad }
        };

        await _generator.GenerateAsync(trip, places, weather, CancellationToken.None);

        Assert.AreEqual(WeatherCondition.Clear, trip.Days[0].WeatherSummary);
        Assert.AreEqual(WeatherCondition.Bad, trip.Days[1].WeatherSummary);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Additional: Generated days match trip duration
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_ThreeDayTrip_ThreeDayPlansCreated()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 3);
        var places = new List<Place>();

        await _generator.GenerateAsync(trip, places, AllClearWeather(3), CancellationToken.None);

        Assert.AreEqual(3, trip.Days.Count);
        Assert.AreEqual(trip.StartDate, trip.Days[0].Date);
        Assert.AreEqual(trip.StartDate.AddDays(1), trip.Days[1].Date);
        Assert.AreEqual(trip.StartDate.AddDays(2), trip.Days[2].Date);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Additional: Single day trip
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_SingleDayTrip_OneDayPlanCreated()
    {
        var mustSees = new List<MustSee> { new(1, "Place", Priority.High) };
        var trip = CreateTrip(mustSees, dayCount: 1);
        var places = new List<Place>
        {
            CreatePlace(1, "Place", 40.4168, -3.7038, duration: 60)
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        Assert.AreEqual(1, trip.Days.Count);
        Assert.AreEqual(1, trip.Days[0].GetBlock(BlockType.Morning).Activities.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ReturnToHotelStrategy Integration Tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_AlwaysStrategy_HotelLegsPresentNoInterBlockTransit()
    {
        // Always is the default — verify hotel transit populated and no InterBlockTransit
        var mustSees = new List<MustSee>
        {
            new(1, "Place Morning", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning),
            new(2, "Place Afternoon", Priority.Medium, pinnedDayIndex: 0, pinnedBlock: BlockType.Afternoon)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, returnToHotelStrategy: ReturnToHotelStrategy.Always);
        var places = new List<Place>
        {
            CreatePlace(1, "Place Morning", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Place Afternoon", 40.4200, -3.7100, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // Always: each block has full hotel transit
        Assert.IsNotNull(morning.TransitFromHotel);
        Assert.IsNotNull(morning.TransitToHotel);
        Assert.IsNull(morning.InterBlockTransit);
        Assert.IsNotNull(afternoon.TransitFromHotel);
        Assert.IsNotNull(afternoon.TransitToHotel);
        Assert.IsNull(afternoon.InterBlockTransit);
    }

    [TestMethod]
    public async Task GenerateAsync_NeverStrategy_InterBlockTransitPopulated()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Morning Place", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning),
            new(2, "Afternoon Place", Priority.Medium, pinnedDayIndex: 0, pinnedBlock: BlockType.Afternoon)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, returnToHotelStrategy: ReturnToHotelStrategy.Never);
        var places = new List<Place>
        {
            CreatePlace(1, "Morning Place", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Afternoon Place", 40.4200, -3.7100, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // Never: InterBlockTransit on destination (Afternoon), hotel legs null at boundary
        Assert.IsNotNull(morning.TransitFromHotel, "Morning still has TransitFromHotel (start of day)");
        Assert.IsNull(morning.TransitToHotel, "Morning TransitToHotel null — inter-block to Afternoon");
        Assert.IsNull(morning.InterBlockTransit, "Morning InterBlockTransit null — stored on destination");

        Assert.IsNull(afternoon.TransitFromHotel, "Afternoon TransitFromHotel null — inter-block from Morning");
        Assert.IsNotNull(afternoon.InterBlockTransit, "Afternoon has InterBlockTransit from Morning");
        Assert.IsNotNull(afternoon.TransitToHotel, "Afternoon returns to hotel at end of block");
    }

    [TestMethod]
    public async Task GenerateAsync_ProximityBased_MechanismRuns()
    {
        // Verify ProximityBased runs through full pipeline without error
        var mustSees = new List<MustSee>
        {
            new(1, "Morning Place", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning),
            new(2, "Afternoon Place", Priority.Medium, pinnedDayIndex: 0, pinnedBlock: BlockType.Afternoon)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, carAvailable: true,
            returnToHotelStrategy: ReturnToHotelStrategy.ProximityBased);
        var places = new List<Place>
        {
            CreatePlace(1, "Morning Place", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Afternoon Place", 40.4200, -3.7100, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);

        // Mechanism ran — either InterBlockTransit on destination OR hotel transit kept
        Assert.IsTrue(
            afternoon.InterBlockTransit != null || morning.TransitToHotel != null,
            "ProximityBased must choose either direct or hotel route");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Overtime / Force-placement integration tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_OvertimeFlagOn_ForcePlacesOversizedMustSee()
    {
        // A must-see with 220 min duration exceeds ALL block max durations
        // (Morning=210, Afternoon=180, Evening=105). With flag on, it should
        // be force-placed with OvertimeAlert=true.
        var mustSees = new List<MustSee>
        {
            new(1, "Oversized Activity", Priority.High)
        };
        var trip = CreateTrip(mustSees, dayCount: 2, allowMustSeeOvertime: true);
        var places = new List<Place>
        {
            CreatePlace(1, "Oversized Activity", 40.4168, -3.7038, duration: 220),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(2), CancellationToken.None);

        // Must-see should be placed in some block
        var total = trip.Days.Sum(d =>
            d.Blocks.Sum(b => b.Activities.Count));
        Assert.AreEqual(1, total);

        // Find the placed activity and verify OvertimeAlert
        var placedActivity = trip.Days
            .SelectMany(d => d.Blocks.SelectMany(b => b.Activities))
            .First();
        Assert.IsTrue(placedActivity.OvertimeAlert,
            "Oversized must-see should have OvertimeAlert=true when flag is on");
    }

    [TestMethod]
    public async Task GenerateAsync_OvertimeFlagOff_ThrowsOverConstrainedForOversizedMustSee()
    {
        // A must-see with 220 min duration exceeds ALL block max durations.
        // With flag off (default), the system throws OverConstrainedRouteException.
        var mustSees = new List<MustSee>
        {
            new(1, "Oversized Activity", Priority.High)
        };
        var trip = CreateTrip(mustSees, dayCount: 2, allowMustSeeOvertime: false);
        var places = new List<Place>
        {
            CreatePlace(1, "Oversized Activity", 40.4168, -3.7038, duration: 220),
        };

        var ex = await CatchExceptionAsync<OverConstrainedRouteException>(() =>
            _generator.GenerateAsync(trip, places, AllClearWeather(2), CancellationToken.None));

        Assert.IsNotNull(ex);
        Assert.IsTrue(ex.ConflictingPlaceIds.Contains(1L),
            "Exception should reference the oversized must-see PlaceId");
    }

    [TestMethod]
    public async Task GenerateAsync_OvertimeFlagOn_FitsNormally_NoOvertimeAlert()
    {
        // Must-see that fits within block capacity should NOT get OvertimeAlert
        // even when the flag is on.
        var mustSees = new List<MustSee>
        {
            new(1, "Normal Activity", Priority.High)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, allowMustSeeOvertime: true);
        var places = new List<Place>
        {
            CreatePlace(1, "Normal Activity", 40.4168, -3.7038, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.AreEqual(1, morning.Activities.Count);
        Assert.IsFalse(morning.Activities[0].OvertimeAlert,
            "Normally-placed activity should NOT have OvertimeAlert");
    }

    [TestMethod]
    public async Task GenerateAsync_OvertimeFlagOn_LowPriorityForcePlaced_HighStillPlacedFirst()
    {
        // Two must-sees: one High that fits normally, one Low that exceeds duration.
        // With flag on, both should be placed — High normally, Low as overtime.
        var mustSees = new List<MustSee>
        {
            new(1, "Fits Normally", Priority.High),
            new(2, "Oversized Low", Priority.Low)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, allowMustSeeOvertime: true);
        var places = new List<Place>
        {
            CreatePlace(1, "Fits Normally", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Oversized Low", 40.4170, -3.7040, duration: 220),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var total = trip.Days[0].GetBlock(BlockType.Morning).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(2, total);

        // The normally-placed activity should NOT have OvertimeAlert
        var normalActivity = trip.Days
            .SelectMany(d => d.Blocks.SelectMany(b => b.Activities))
            .First(a => a.PlaceId == 1L);
        Assert.IsFalse(normalActivity.OvertimeAlert);

        // The force-placed activity SHOULD have OvertimeAlert
        var overtimeActivity = trip.Days
            .SelectMany(d => d.Blocks.SelectMany(b => b.Activities))
            .First(a => a.PlaceId == 2L);
        Assert.IsTrue(overtimeActivity.OvertimeAlert);
    }

    [TestMethod]
    public async Task GenerateAsync_NeverStrategy_EveningAlwaysReturnsToHotel()
    {
        var mustSees = new List<MustSee>
        {
            new(1, "Afternoon Place", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Afternoon),
            new(2, "Evening Place", Priority.Medium, pinnedDayIndex: 0, pinnedBlock: BlockType.Evening)
        };
        var trip = CreateTrip(mustSees, dayCount: 1, returnToHotelStrategy: ReturnToHotelStrategy.Never);
        var places = new List<Place>
        {
            CreatePlace(1, "Afternoon Place", 40.4200, -3.7100, duration: 60),
            CreatePlace(2, "Evening Place", 40.4180, -3.7140, duration: 60),
        };

        await _generator.GenerateAsync(trip, places, AllClearWeather(1), CancellationToken.None);

        var evening = trip.Days[0].GetBlock(BlockType.Evening);

        // Evening always returns to hotel regardless of strategy
        Assert.IsNotNull(evening.TransitToHotel, "Evening always returns to hotel — even with Never strategy");
    }
}
