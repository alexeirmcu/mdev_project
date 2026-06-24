using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Services;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class ItineraryReplanningEngineTests
{
    private sealed class StubCandidateFiller : ICandidateFiller
    {
        public bool FillScopedCalled { get; private set; }
        public HashSet<long>? LastExcludePlaceIds { get; private set; }
        public ReplanScope? LastScope { get; private set; }

        public Task FillAsync(Trip trip, List<Place> candidatePool, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
            => Task.CompletedTask;

        public Task FillScopedAsync(Trip trip, ReplanScope scope, List<Place> candidatePool, HashSet<long> excludePlaceIds, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
        {
            FillScopedCalled = true;
            LastScope = scope;
            LastExcludePlaceIds = new HashSet<long>(excludePlaceIds);

            // For testing weather swap: add indoor candidates to empty slots if available
            foreach (var dayPlan in trip.Days)
            {
                foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
                {
                    var block = dayPlan.GetBlock(blockType);
                    var remainingCapacity = 3 - block.Activities.Count;
                    if (remainingCapacity <= 0) continue;

                    var available = candidatePool
                        .Where(p => !excludePlaceIds.Contains(p.Id))
                        .Take(remainingCapacity)
                        .ToList();

                    foreach (var place in available)
                    {
                        var activity = CreateActivityNodeForTest(place, block.Activities.Count + 1);
                        dayPlan.AddActivity(blockType, activity);
                        excludePlaceIds.Add(place.Id);
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static ActivityNode CreateActivityNodeForTest(Place place, int sequenceOrder)
        {
            return new ActivityNode(
                place.Id,
                place.Name,
                sequenceOrder,
                place.TypicalDurationMinutes,
                place.IsIndoor,
                location: place.Location ?? new PlaceLocation(0, 0));
        }
    }

    private sealed class StubTransitEnricher : ITransitEnricher
    {
        public bool EnrichScopedCalled { get; private set; }
        public ReplanScope? LastScope { get; private set; }

        public Task EnrichAsync(Trip trip, IReadOnlyDictionary<long, Place> placesById, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
            => Task.CompletedTask;

        public Task EnrichScopedAsync(Trip trip, ReplanScope scope, IReadOnlyDictionary<long, Place> placesById, Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct)
        {
            EnrichScopedCalled = true;
            LastScope = scope;

            // Set weather from data
            foreach (var dayPlan in trip.Days)
            {
                if (weatherData.TryGetValue(dayPlan.Date, out var w))
                    dayPlan.SetWeather(w);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubTimelineScheduler : ITimelineScheduler
    {
        public bool ScheduleScopedCalled { get; private set; }
        public List<int>? LastDayIndices { get; private set; }

        public void Schedule(Trip trip) { }

        public void ScheduleScoped(Trip trip, List<int> dayIndices, int seedPreviousBlockEnd)
        {
            ScheduleScopedCalled = true;
            LastDayIndices = new List<int>(dayIndices);
        }
    }

    private static Place CreatePlace(long id, string name, bool isIndoor = false, int duration = 60)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(40.4168 + id * 0.01, -3.7038 + id * 0.01),
            duration, isIndoor, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);
        return place;
    }

    private static ActivityNode CreateActivity(long placeId, string name, int sequence = 1,
        bool isCompleted = false, bool isIndoor = false, Priority priority = Priority.Medium)
    {
        var activity = new ActivityNode(
            placeId,
            name,
            sequence,
            60,
            isIndoor,
            location: new PlaceLocation(40.4168, -3.7038))
        {
            Priority = priority
        };
        if (isCompleted)
            activity.SetCompleted(true);
        return activity;
    }

    private static Trip CreateTripWithDays(int dayCount = 3)
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
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };

        trip.GenerateDays();
        return trip;
    }

    private static void AddActivityToDay(Trip trip, int dayIndex, BlockType blockType, ActivityNode activity)
    {
        trip.Days[dayIndex].GetBlock(blockType).Activities.Add(activity);
    }

    // ============================================================
    // RegenerateDayAsync Tests
    // ============================================================

    [TestMethod]
    public async Task RegenerateDayAsync_PreservesCompletedActivities()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var day1 = trip.Days[1];

        var completed = CreateActivity(1, "Completed A", isCompleted: true);
        var nonCompleted = CreateActivity(2, "NonCompleted B", isCompleted: false);
        AddActivityToDay(trip, 1, BlockType.Morning, completed);
        AddActivityToDay(trip, 1, BlockType.Afternoon, nonCompleted);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[1].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.RegenerateDayAsync(trip, 1, new List<Place>(), weather, CancellationToken.None);

        // Assert
        Assert.IsTrue(day1.GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 1), "Completed activity should remain");
        Assert.IsFalse(day1.GetBlock(BlockType.Afternoon).Activities.Any(a => a.PlaceId == 2), "Non-completed, non-must-see should be removed");
    }

    [TestMethod]
    public async Task RegenerateDayAsync_PreservesMustSees()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 1);
        trip.AddMustSee(mustSee);

        var mustSeeActivity = CreateActivity(1, "MustSee Place");
        AddActivityToDay(trip, 1, BlockType.Morning, mustSeeActivity);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[1].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.RegenerateDayAsync(trip, 1, new List<Place>(), weather, CancellationToken.None);

        // Assert
        var morning = trip.Days[1].GetBlock(BlockType.Morning);
        Assert.IsTrue(morning.Activities.Any(a => a.PlaceId == 1), "Must-see should remain");
    }

    [TestMethod]
    public async Task RegenerateDayAsync_ClearsNonCompletedNonMustSees()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var mustSee = new MustSee(3, Priority.High);
        trip.AddMustSee(mustSee);

        var completed = CreateActivity(1, "Completed", isCompleted: true);
        var toRemove = CreateActivity(2, "RemoveMe");
        var mustSeeActivity = CreateActivity(3, "MustSee");
        AddActivityToDay(trip, 1, BlockType.Morning, completed);
        AddActivityToDay(trip, 1, BlockType.Morning, toRemove);
        AddActivityToDay(trip, 1, BlockType.Morning, mustSeeActivity);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[1].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.RegenerateDayAsync(trip, 1, new List<Place>(), weather, CancellationToken.None);

        // Assert
        var morning = trip.Days[1].GetBlock(BlockType.Morning);
        Assert.IsTrue(morning.Activities.Any(a => a.PlaceId == 1), "Completed should remain");
        Assert.IsFalse(morning.Activities.Any(a => a.PlaceId == 2), "Non-completed, non-must-see should be cleared");
        Assert.IsTrue(morning.Activities.Any(a => a.PlaceId == 3), "Must-see should remain");

        // The filler should have been called with exclusions that include placeIds 1 and 3
        Assert.IsTrue(filler.FillScopedCalled, "FillScopedAsync should be called");
        Assert.IsTrue(filler.LastExcludePlaceIds!.Contains(1), "Completed placeId should be excluded");
        Assert.IsTrue(filler.LastExcludePlaceIds.Contains(3), "Must-see placeId should be excluded");
    }

    [TestMethod]
    public async Task RegenerateDayAsync_ClearsStale()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        trip.Days[1].MarkStale();
        Assert.IsTrue(trip.Days[1].IsStale, "Precondition: day should be stale");

        // Add a completed activity so the day isn't empty
        var completed = CreateActivity(1, "Completed", isCompleted: true);
        AddActivityToDay(trip, 1, BlockType.Morning, completed);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[1].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.RegenerateDayAsync(trip, 1, new List<Place>(), weather, CancellationToken.None);

        // Assert
        Assert.IsFalse(trip.Days[1].IsStale, "Stale should be cleared after regeneration");
    }

    [TestMethod]
    public async Task RegenerateDayAsync_AllCompleted_NoOp()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var completed1 = CreateActivity(1, "Completed A", isCompleted: true);
        var completed2 = CreateActivity(2, "Completed B", isCompleted: true);
        AddActivityToDay(trip, 1, BlockType.Morning, completed1);
        AddActivityToDay(trip, 1, BlockType.Afternoon, completed2);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[1].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.RegenerateDayAsync(trip, 1, new List<Place>(), weather, CancellationToken.None);

        // Assert — filler called but no new candidates added (candidates list is empty)
        Assert.IsTrue(filler.FillScopedCalled, "FillScoped should be called even with all completed (enrich/schedule still run)");

        // Other days untouched
        var otherDay = trip.Days[0];
        Assert.AreEqual(0, otherDay.GetBlock(BlockType.Morning).Activities.Count);
        Assert.AreEqual(0, otherDay.GetBlock(BlockType.Afternoon).Activities.Count);
        Assert.AreEqual(0, otherDay.GetBlock(BlockType.Evening).Activities.Count);
    }

    // ============================================================
    // ReplanAsync Tests
    // ============================================================

    [TestMethod]
    public async Task ReplanAsync_CurrentBlock_OnlyMutatesCurrentBlock()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var act1 = CreateActivity(1, "MorningAct", isCompleted: false, isIndoor: false);
        var act2 = CreateActivity(2, "AfternoonAct", isCompleted: false, isIndoor: false);
        AddActivityToDay(trip, 1, BlockType.Morning, act1);
        AddActivityToDay(trip, 1, BlockType.Afternoon, act2);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var context = new ReplanContext(
            CurrentDayIndex: 1,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.CurrentBlock,
            IsBadWeather: true,
            CurrentDateTime: new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[1].Date, WeatherCondition.Bad }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place>(), weather, CancellationToken.None);

        // Assert: other blocks untouched
        var day1Morning = trip.Days[1].GetBlock(BlockType.Morning);
        var day1Afternoon = trip.Days[1].GetBlock(BlockType.Afternoon);

        // Morning was in scope and bad weather triggers outdoor swap
        Assert.IsFalse(day1Morning.Activities.Any(a => a.PlaceId == 1),
            "Current block outdoor non-completed/non-must-see should be cleared in bad weather");
        Assert.IsTrue(day1Afternoon.Activities.Any(a => a.PlaceId == 2),
            "Afternoon block should be untouched (not in scope)");
    }

    [TestMethod]
    public async Task ReplanAsync_RemainingTrip_MutatesAllForward()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var day0Act = CreateActivity(1, "Day0Act", isCompleted: false, isIndoor: false);
        var day1Act = CreateActivity(2, "Day1Act", isCompleted: false, isIndoor: false);
        var day2Act = CreateActivity(3, "Day2Act", isCompleted: false, isIndoor: false);
        AddActivityToDay(trip, 0, BlockType.Morning, day0Act);
        AddActivityToDay(trip, 1, BlockType.Morning, day1Act);
        AddActivityToDay(trip, 2, BlockType.Morning, day2Act);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var context = new ReplanContext(
            CurrentDayIndex: 1,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.RemainingTrip,
            IsBadWeather: true,
            CurrentDateTime: new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Clear },
            { trip.Days[1].Date, WeatherCondition.Bad },
            { trip.Days[2].Date, WeatherCondition.Bad }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place>(), weather, CancellationToken.None);

        // Assert: Day 0 is untouched (past day, fully locked)
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 1),
            "Day 0 should be untouched (past day)");

        // Day 1 Morning is in scope with bad weather: outdoor non-completed/non-must-see should be swapped
        Assert.IsFalse(trip.Days[1].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 2),
            "Day 1 morning should be in scope and outdoor activity cleared in bad weather");

        // Day 2 is in scope with bad weather
        Assert.IsFalse(trip.Days[2].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 3),
            "Day 2 should be in scope and outdoor activity cleared in bad weather");
    }

    [TestMethod]
    public async Task ReplanAsync_PreservesCompletedAcrossDays()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var completed0 = CreateActivity(1, "CompletedDay0", isCompleted: true);
        var completed1 = CreateActivity(2, "CompletedDay1", isCompleted: true);
        var nonCompleted = CreateActivity(3, "NonCompleted", isCompleted: false);
        AddActivityToDay(trip, 0, BlockType.Morning, completed0);
        AddActivityToDay(trip, 1, BlockType.Morning, completed1);
        AddActivityToDay(trip, 1, BlockType.Afternoon, nonCompleted);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var context = new ReplanContext(
            CurrentDayIndex: 1,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.RemainingTrip,
            IsBadWeather: false,
            CurrentDateTime: new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Clear },
            { trip.Days[1].Date, WeatherCondition.Clear },
            { trip.Days[2].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place>(), weather, CancellationToken.None);

        // Assert: completed activities preserved across all days
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 1),
            "Completed activity in day 0 should be preserved");
        Assert.IsTrue(trip.Days[1].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 2),
            "Completed activity in day 1 should be preserved");
    }

    [TestMethod]
    public async Task ReplanAsync_BadWeather_SwapsOutdoorToIndoor()
    {
        // Arrange
        var trip = CreateTripWithDays(1);
        var outdoor = CreateActivity(1, "OutdoorPlace", isCompleted: false, isIndoor: false);
        AddActivityToDay(trip, 0, BlockType.Morning, outdoor);

        var indoorPlace = CreatePlace(2, "IndoorPlace", isIndoor: true);
        var outdoorPlace = CreatePlace(3, "AnotherOutdoor", isIndoor: false);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var context = new ReplanContext(
            CurrentDayIndex: 0,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.CurrentDay,
            IsBadWeather: true,
            CurrentDateTime: new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Bad }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place> { indoorPlace, outdoorPlace }, weather, CancellationToken.None);

        // Assert: outdoor non-completed, non-must-see should be removed
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.IsFalse(morning.Activities.Any(a => a.PlaceId == 1),
            "Outdoor non-completed activity should be removed in bad weather");

        // The indoor candidate should have been filled (through stub filler which adds from pool)
        // Our stub filler adds available candidates, but we need to check what was filled
        Assert.IsTrue(filler.FillScopedCalled, "Filler should be called");
    }

    [TestMethod]
    public async Task ReplanAsync_BadWeather_KeepsForcedMustSee()
    {
        // Arrange
        var trip = CreateTripWithDays(1);
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 0,
            pinnedBlock: BlockType.Morning, forceIncludeDespiteWeather: true);
        trip.AddMustSee(mustSee);

        var forcedOutdoor = CreateActivity(1, "ForcedOutdoor", isCompleted: false, isIndoor: false);
        AddActivityToDay(trip, 0, BlockType.Morning, forcedOutdoor);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var context = new ReplanContext(
            CurrentDayIndex: 0,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.CurrentDay,
            IsBadWeather: true,
            CurrentDateTime: new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Bad }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place>(), weather, CancellationToken.None);

        // Assert: forced must-see outdoor stays despite bad weather
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 1),
            "Forced outdoor must-see should remain in bad weather");
    }

    [TestMethod]
    public async Task ReplanAsync_PrunesLowPriorityWhenOverCapacity()
    {
        // Arrange
        var trip = CreateTripWithDays(1);

        // Add many activities: completed, high, low
        var completed = CreateActivity(1, "Completed", isCompleted: true);
        var high = CreateActivity(2, "HighPriority", isCompleted: false, priority: Priority.High);
        var low = CreateActivity(3, "LowPriority", isCompleted: false, priority: Priority.Low);

        AddActivityToDay(trip, 0, BlockType.Morning, completed);
        AddActivityToDay(trip, 0, BlockType.Morning, high);
        AddActivityToDay(trip, 0, BlockType.Morning, low);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        // Current time is past planned start -> behind schedule
        var context = new ReplanContext(
            CurrentDayIndex: 0,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.CurrentDay,
            IsBadWeather: false,
            CurrentDateTime: new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place>(), weather, CancellationToken.None);

        // Assert: completed and high remain, low non-must-see is pruned when behind schedule
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        Assert.IsTrue(morning.Activities.Any(a => a.PlaceId == 1), "Completed should remain");
        Assert.IsTrue(morning.Activities.Any(a => a.PlaceId == 2), "High priority should remain");
        Assert.IsFalse(morning.Activities.Any(a => a.PlaceId == 3), "Low priority should be pruned when behind schedule");
    }

    [TestMethod]
    public async Task ReplanAsync_NoRemainingActivities_NoOp()
    {
        // Arrange
        var trip = CreateTripWithDays(3);
        var completed0 = CreateActivity(1, "CompletedDay0", isCompleted: true);
        var completed1 = CreateActivity(2, "CompletedDay1", isCompleted: true);
        var completed2a = CreateActivity(3, "CompletedDay2a", isCompleted: true);
        var completed2b = CreateActivity(4, "CompletedDay2b", isCompleted: true);
        AddActivityToDay(trip, 0, BlockType.Morning, completed0);
        AddActivityToDay(trip, 1, BlockType.Morning, completed1);
        AddActivityToDay(trip, 2, BlockType.Morning, completed2a);
        AddActivityToDay(trip, 2, BlockType.Afternoon, completed2b);

        var filler = new StubCandidateFiller();
        var enricher = new StubTransitEnricher();
        var scheduler = new StubTimelineScheduler();
        var engine = new ItineraryReplanningEngine(filler, enricher, scheduler);

        var context = new ReplanContext(
            CurrentDayIndex: 0,
            CurrentBlock: BlockType.Morning,
            Scope: ReplanScope.RemainingTrip,
            IsBadWeather: false,
            CurrentDateTime: new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.Days[0].Date, WeatherCondition.Clear },
            { trip.Days[1].Date, WeatherCondition.Clear },
            { trip.Days[2].Date, WeatherCondition.Clear }
        };

        // Act
        await engine.ReplanAsync(trip, context, new List<Place>(), weather, CancellationToken.None);

        // Assert: everything still there, filler was still called (enrich/schedule always runs)
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 1));
        Assert.IsTrue(trip.Days[1].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 2));
        Assert.IsTrue(trip.Days[2].GetBlock(BlockType.Morning).Activities.Any(a => a.PlaceId == 3));
        Assert.IsTrue(trip.Days[2].GetBlock(BlockType.Afternoon).Activities.Any(a => a.PlaceId == 4));
        Assert.IsTrue(filler.FillScopedCalled, "FillScoped should be called");
    }
}
