using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Services;
using SmartTripPlanner.Domain.Constants;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class TimelineSchedulerTests
{
    private static PlaceLocation MadridCenter => new(40.4168, -3.7038);
    private static PlaceLocation NearLocation => new(40.4170, -3.7040);

    private static TransitDetails CreateTransit(int durationMinutes, int bufferMinutes = 5, bool frictionAlert = false)
        => new(TransportMode.WALK_AND_PUBLIC_TRANSPORT, durationMinutes, bufferMinutes, frictionAlert);

    private static ActivityNode CreateActivity(long id, int durationMinutes, TransitDetails? transitToNext = null)
        => new(id, $"Activity {id}", (int)id, durationMinutes, location: NearLocation)
        {
            TransitToNext = transitToNext,
            EstimatedArrival = 0,
            EstimatedDeparture = 0
        };

    private static BlockTimeline CreateBlock(BlockType blockType, params ActivityNode[] activities)
    {
        var block = new BlockTimeline { BlockType = blockType };
        foreach (var activity in activities)
            block.AddActivity(activity);
        return block;
    }

    private static Trip CreateTripWithDay(BlockTimeline morning, BlockTimeline? afternoon = null, BlockTimeline? evening = null)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "TEST",
            CityId = 1,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 1),
            BaseHotel = new Location("Hotel", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var day = new DayPlan(
            0,
            trip.StartDate,
            morning,
            afternoon ?? new BlockTimeline { BlockType = BlockType.Afternoon },
            evening ?? new BlockTimeline { BlockType = BlockType.Evening }
        );
        day.SetWeather(WeatherCondition.Clear);
        day.UpdateStartTime(trip.DefaultStartTime);

        // Replace _days via reflection to avoid needing GenerateDaysFrom
        var daysField = typeof(Trip).GetField("_days",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        daysField.SetValue(trip, new List<DayPlan> { day });

        return trip;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Scenario: Single activity block gets arrival and departure
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_SingleActivityWithHotelTransit_SetsArrivalAndDeparture()
    {
        // Morning block, DayPlan.StartTime = 09:00 (540 min from midnight)
        // TransitFromHotel = 15 min, Buffer = 5 min, Activity duration = 60 min
        var activity = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activity);
        morning.TransitFromHotel = CreateTransit(15, 5);

        var trip = CreateTripWithDay(morning);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // EstimatedArrival = 540 + 15 + 5 = 560
        // EstimatedDeparture = 560 + 60 = 620
        Assert.AreEqual(560, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(620, morning.Activities[0].EstimatedDeparture);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Scenario: Multiple activities advance time by duration plus transit
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_MultipleActivities_AdvancesTimeByDurationPlusTransit()
    {
        // TransitFromHotel = 15 min, Buffer = 0
        // Activity[A]: 60 min, TransitToNext: 10 min + 5 buffer
        // Activity[B]: 45 min
        var activityA = CreateActivity(1, 60, CreateTransit(10, 5));
        var activityB = CreateActivity(2, 45);
        var morning = CreateBlock(BlockType.Morning, activityA, activityB);
        morning.TransitFromHotel = CreateTransit(15, 0);

        var trip = CreateTripWithDay(morning);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // A.EstimatedArrival = 540 + 15 + 0 = 555
        // A.EstimatedDeparture = 555 + 60 = 615
        // B.EstimatedArrival = 615 + 10 + 5 = 630
        // B.EstimatedDeparture = 630 + 45 = 675
        Assert.AreEqual(555, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(615, morning.Activities[0].EstimatedDeparture);
        Assert.AreEqual(630, morning.Activities[1].EstimatedArrival);
        Assert.AreEqual(675, morning.Activities[1].EstimatedDeparture);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Scenario: Empty block is skipped
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_EmptyBlock_NoArrivalOrDepartureSet()
    {
        var morning = CreateBlock(BlockType.Morning);
        var trip = CreateTripWithDay(morning);
        var scheduler = new TimelineScheduler();

        // Should not throw — empty block has no activities to schedule
        scheduler.Schedule(trip);

        Assert.AreEqual(0, morning.Activities.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Scenario: Each block starts at DayPlan.StartTime (MVP)
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_MultipleBlocks_EachStartsAtDayPlanStartTime()
    {
        var activityMorning = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activityMorning);
        morning.TransitFromHotel = CreateTransit(10, 0);

        var activityAfternoon = CreateActivity(2, 45);
        var afternoon = CreateBlock(BlockType.Afternoon, activityAfternoon);
        afternoon.TransitFromHotel = CreateTransit(5, 0);

        var trip = CreateTripWithDay(morning, afternoon);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Morning first activity: 540 + 10 + 0 = 550
        Assert.AreEqual(550, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(610, morning.Activities[0].EstimatedDeparture);

        // Afternoon first activity also starts at DayPlan.StartTime (540 + 5 + 0 = 545)
        // NOT chained from morning's end
        Assert.AreEqual(545, afternoon.Activities[0].EstimatedArrival);
        Assert.AreEqual(590, afternoon.Activities[0].EstimatedDeparture);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Scenario: Block without hotel transit starts at DayPlan.StartTime directly
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_BlockWithoutHotelTransit_StartsAtDayPlanStartTime()
    {
        var activity = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activity);
        // TransitFromHotel is null

        var trip = CreateTripWithDay(morning);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // EstimatedArrival = 540 (DayPlan.StartTime, no hotel transit added)
        Assert.AreEqual(540, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(600, morning.Activities[0].EstimatedDeparture);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Scenario: Block with TransitToHotel — does NOT affect next block
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_TransitToHotel_DoesNotAffectNextBlockStart()
    {
        var activityMorning = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activityMorning);
        morning.TransitFromHotel = CreateTransit(10, 5);
        morning.TransitToHotel = CreateTransit(20, 5);

        var activityAfternoon = CreateActivity(2, 45);
        var afternoon = CreateBlock(BlockType.Afternoon, activityAfternoon);
        afternoon.TransitFromHotel = CreateTransit(5, 0);

        var trip = CreateTripWithDay(morning, afternoon);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Morning last activity departure includes TransitToHotel? NO — TransitToHotel is not
        // included in activity time advancement. Only TransitFromHotel affects the block start.
        // Morning should end at: 540 + 15 + 60 = 615 (activity end, TransitToHotel not part of
        // the activity timeline)

        // Afternoon starts at DayPlan.StartTime (540) not chained from morning
        // Afternoon first activity: 540 + 5 + 0 = 545
        Assert.AreEqual(545, afternoon.Activities[0].EstimatedArrival,
            "Afternoon should start at DayPlan.StartTime + TransitFromHotel, not chained from morning");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Block chaining via InterBlockTransit tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Schedule_BlockWithInterBlockTransit_ChainsFromPreviousBlockEnd()
    {
        // Morning: TransitFromHotel=15+5, Activity=60 → morning end = 540+20+60 = 620
        // InterBlockTransit: 10+5 (stored on destination block — Afternoon)
        // Afternoon: starts at 620+15 = 635, Activity=45 → departure = 680
        var activityA = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activityA);
        morning.TransitFromHotel = CreateTransit(15, 5);

        var activityB = CreateActivity(2, 45);
        var afternoon = CreateBlock(BlockType.Afternoon, activityB);
        afternoon.InterBlockTransit = CreateTransit(10, 5); // stored on destination block

        var trip = CreateTripWithDay(morning, afternoon);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Morning first activity: 540 + 15 + 5 = 560
        Assert.AreEqual(560, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(620, morning.Activities[0].EstimatedDeparture);

        // Afternoon first activity chains from morning end + InterBlockTransit
        // 620 + 10 + 5 = 635
        Assert.AreEqual(635, afternoon.Activities[0].EstimatedArrival,
            "Afternoon should chain from morning end via InterBlockTransit");
        Assert.AreEqual(680, afternoon.Activities[0].EstimatedDeparture);
    }

    [TestMethod]
    public void Schedule_BlockWithTransitFromHotel_ResetsToStartTime()
    {
        // Morning has InterBlockTransit (but also TransitFromHotel)
        // TransitFromHotel takes priority → reset to DayPlan.StartTime
        var activityA = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activityA);
        morning.TransitFromHotel = CreateTransit(10, 0);
        morning.InterBlockTransit = CreateTransit(5, 0);

        var trip = CreateTripWithDay(morning);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Should start at DayPlan.StartTime (540) + TransitFromHotel (10) = 550
        Assert.AreEqual(550, morning.Activities[0].EstimatedArrival,
            "TransitFromHotel takes priority over InterBlockTransit — starts at StartTime");
    }

    [TestMethod]
    public void Schedule_InterBlockTransitChainsMixedWithTransitFromHotel()
    {
        // Morning: TransitFromHotel=10+0, Activity=60 → end = 540+10+60 = 610
        // InterBlockTransit: 10+5 (on Afternoon) → Afternoon chains at 610+15 = 625
        // Afternoon: no TransitFromHotel, Activity=45 → end = 625+45 = 670
        // No InterBlockTransit to Evening → Evening has TransitFromHotel
        // Evening: TransitFromHotel=5+0, Activity=30 → start = 540+5 = 545
        var activityM = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activityM);
        morning.TransitFromHotel = CreateTransit(10, 0);

        var activityA = CreateActivity(2, 45);
        var afternoon = CreateBlock(BlockType.Afternoon, activityA);
        afternoon.InterBlockTransit = CreateTransit(10, 5); // from Morning to Afternoon

        var activityE = CreateActivity(3, 30);
        var evening = CreateBlock(BlockType.Evening, activityE);
        evening.TransitFromHotel = CreateTransit(5, 0);

        var trip = CreateTripWithDay(morning, afternoon, evening);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Morning: 540 + 10 = 550, end = 610
        Assert.AreEqual(550, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(610, morning.Activities[0].EstimatedDeparture);

        // Afternoon chains from morning: 610 + 10 + 5 = 625
        Assert.AreEqual(625, afternoon.Activities[0].EstimatedArrival);
        Assert.AreEqual(670, afternoon.Activities[0].EstimatedDeparture);

        // Evening has TransitFromHotel → resets to StartTime: 540 + 5 = 545
        Assert.AreEqual(545, evening.Activities[0].EstimatedArrival,
            "Evening with TransitFromHotel should reset to StartTime, not chain from afternoon");
        Assert.AreEqual(575, evening.Activities[0].EstimatedDeparture);
    }

    [TestMethod]
    public void Schedule_EmptyBlockFollowedByNonEmpty_ResetsToStartTime()
    {
        // Morning: empty
        // Afternoon: TransitFromHotel=10+0, Activity=45
        // Since morning is empty, no InterBlockTransit → Afternoon resets to StartTime
        var morning = CreateBlock(BlockType.Morning); // empty

        var activityA = CreateActivity(2, 45);
        var afternoon = CreateBlock(BlockType.Afternoon, activityA);
        afternoon.TransitFromHotel = CreateTransit(10, 0);

        var trip = CreateTripWithDay(morning, afternoon);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Morning skipped (empty)
        Assert.AreEqual(0, morning.Activities.Count);

        // Afternoon starts at DayPlan.StartTime (540) + TransitFromHotel (10) = 550
        Assert.AreEqual(550, afternoon.Activities[0].EstimatedArrival,
            "Afternoon after empty Morning should reset to StartTime");
    }

    [TestMethod]
    public void Schedule_MultipleInterBlockTransit_ChainsCorrectly()
    {
        // Three blocks all chained via InterBlockTransit (no TransitFromHotel)
        // Morning: Activity=60 → end=600
        // InterBlockTransit: 10+5 (on Afternoon) → Afternoon starts at 615
        // Afternoon: Activity=45 → end=660
        // InterBlockTransit: 8+3 (on Evening) → Evening starts at 671
        // Evening: Activity=30 → end=701
        var activityM = CreateActivity(1, 60);
        var morning = CreateBlock(BlockType.Morning, activityM);

        var activityA = CreateActivity(2, 45);
        var afternoon = CreateBlock(BlockType.Afternoon, activityA);
        afternoon.InterBlockTransit = CreateTransit(10, 5); // from Morning to Afternoon

        var activityE = CreateActivity(3, 30);
        var evening = CreateBlock(BlockType.Evening, activityE);
        evening.InterBlockTransit = CreateTransit(8, 3); // from Afternoon to Evening

        var trip = CreateTripWithDay(morning, afternoon, evening);
        var scheduler = new TimelineScheduler();

        scheduler.Schedule(trip);

        // Morning: 540 + 0 = 540 (no TransitFromHotel, first block)
        Assert.AreEqual(540, morning.Activities[0].EstimatedArrival);
        Assert.AreEqual(600, morning.Activities[0].EstimatedDeparture);

        // Afternoon: 600 + 10 + 5 = 615
        Assert.AreEqual(615, afternoon.Activities[0].EstimatedArrival);
        Assert.AreEqual(660, afternoon.Activities[0].EstimatedDeparture);

        // Evening: 660 + 8 + 3 = 671
        Assert.AreEqual(671, evening.Activities[0].EstimatedArrival);
        Assert.AreEqual(701, evening.Activities[0].EstimatedDeparture);
    }
}
