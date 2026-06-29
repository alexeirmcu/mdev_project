using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Services;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class PinnedMustSeePlacerTests
{
    private readonly PinnedMustSeePlacer _placer = new();

    private static Place CreatePlace(long id, string name, double lat, double lng, int duration = 60)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng), duration, false, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));

        return place;
    }

    private static Trip CreateTrip(IReadOnlyList<MustSee> mustSees, int dayCount = 3)
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

        foreach (var ms in mustSees)
            trip.AddMustSee(ms);

        // Pre-generate days manually to test placement without running full generator
        trip.GenerateDaysFrom(trip.StartDate);

        return trip;
    }

    [TestMethod]
    public void Place_PinnedMustSeeWithBlock_PlacesInCorrectDayAndBlock()
    {
        var mustSee = new MustSee(1, "Museo del Prado", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 2);
        var place = CreatePlace(1, "Museo del Prado", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        Assert.AreEqual(1, trip.Days[0].GetBlock(BlockType.Morning).Activities.Count);
        Assert.AreEqual(1L, trip.Days[0].GetBlock(BlockType.Morning).Activities[0].PlaceId);
    }

    [TestMethod]
    public void Place_PinnedMustSeeNoBlock_TriesMorningFirst()
    {
        var mustSee = new MustSee(1, "Place", Priority.High, pinnedDayIndex: 0);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place = CreatePlace(1, "Place", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        // Should end up in one of the three blocks
        var total = trip.Days[0].GetBlock(BlockType.Morning).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count
                  + trip.Days[0].GetBlock(BlockType.Evening).Activities.Count;
        Assert.AreEqual(1, total);
    }

    [TestMethod]
    public void Place_InvalidDayIndex_ReturnsFalse()
    {
        var mustSee = new MustSee(1, "Place", Priority.High, pinnedDayIndex: 99);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place = CreatePlace(1, "Place", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Place_FullTargetBlock_OverflowsToAdjacentBlock()
    {
        // Fill Morning to capacity, then try to place another
        var mustSee = new MustSee(1, "Place 1", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place1 = CreatePlace(1, "Place 1", 40.4168, -3.7038, duration: 60);
        var place2 = CreatePlace(2, "Place 2", 40.4170, -3.7040, duration: 60);

        // Fill morning to max via trip.AddActivity
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        morning.AddActivity(new ActivityNode(1, "Existing", 1, 60, location: place1.Location));
        morning.AddActivity(new ActivityNode(2, "Existing 2", 2, 60, location: place2.Location));
        morning.AddActivity(new ActivityNode(3, "Existing 3", 3, 60, location: new PlaceLocation(40.4180, -3.7050)));

        // Now try to place a pinned must-see in Morning — should overflow to Afternoon
        var place3 = CreatePlace(4, "Overflow Place", 40.4190, -3.7060);
        var mustSee2 = new MustSee(4, "Overflow Place", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);

        var result = _placer.Place(trip, mustSee2, place3);

        Assert.IsTrue(result, "Pinned must-see should overflow to adjacent block");
        Assert.AreEqual(1, trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count,
            "Overflow should go to afternoon (adjacent to morning)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Overtime / Force-placement tests
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Place_WithOvertimeFlagOn_ForcePlacesWhenDurationExceeds()
    {
        // Must-see with 220 min duration exceeds Morning max (210).
        // With AllowMustSeeOvertime=true, should force-place with OvertimeAlert.
        var mustSee = new MustSee(1, "Long Activity", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        // Set AllowMustSeeOvertime to true
        typeof(Trip).GetProperty("Preferences")!.SetValue(trip,
            new TripPreferences(allowMustSeeOvertime: true));
        var place = CreatePlace(1, "Long Activity", 40.4168, -3.7038, duration: 220);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result, "Should force-place oversized must-see with flag on");
        Assert.AreEqual(1, trip.Days[0].GetBlock(BlockType.Morning).Activities.Count);
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Morning).Activities[0].OvertimeAlert,
            "Force-placed activity should have OvertimeAlert=true");
    }

    [TestMethod]
    public void Place_WithOvertimeFlagOff_ReturnsFalseWhenDurationExceeds()
    {
        // Must-see with 220 min duration exceeds Morning max (210).
        // With AllowMustSeeOvertime=false (default), should return false.
        var mustSee = new MustSee(1, "Long Activity", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place = CreatePlace(1, "Long Activity", 40.4168, -3.7038, duration: 220);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result, "Should not place oversized must-see with flag off");
        Assert.AreEqual(0, trip.Days[0].GetBlock(BlockType.Morning).Activities.Count);
    }

    [TestMethod]
    public void Place_WithOvertimeFlagOn_ForcePlacesInAdjacentBlock()
    {
        // Fill Morning to max visits, then try to place an oversized pinned must-see.
        // Normal overflow to adjacent Afternoon should fail (duration),
        // but force-placement should succeed with OvertimeAlert.
        var mustSee = new MustSee(1, "Must-See", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        typeof(Trip).GetProperty("Preferences")!.SetValue(trip,
            new TripPreferences(allowMustSeeOvertime: true));
        var loc = new PlaceLocation(40.4168, -3.7038);

        // Fill Morning to max visits
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        morning.AddActivity(new ActivityNode(1, "Existing 1", 1, 60, location: loc));
        morning.AddActivity(new ActivityNode(2, "Existing 2", 2, 60, location: loc));
        morning.AddActivity(new ActivityNode(3, "Existing 3", 3, 60, location: loc));

        // Now place an oversized must-see that won't fit duration in Afternoon
        // Afternoon max is 180 min — 200 min exceeds it
        var place = CreatePlace(4, "Oversized Place", 40.4190, -3.7060, duration: 200);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result, "Should force-place in adjacent block with flag on");
        Assert.AreEqual(1, trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count,
            "Force-placed activity should be in Afternoon (adjacent to Morning)");
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Afternoon).Activities[0].OvertimeAlert,
            "Force-placed activity should have OvertimeAlert=true");
    }

    [TestMethod]
    public void Place_WithOvertimeFlagOn_FitsNormally_NoOvertimeAlert()
    {
        // Must-see that fits normally — OvertimeAlert should remain false
        var mustSee = new MustSee(1, "Normal Activity", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        typeof(Trip).GetProperty("Preferences")!.SetValue(trip,
            new TripPreferences(allowMustSeeOvertime: true));
        var place = CreatePlace(1, "Normal Activity", 40.4168, -3.7038, duration: 60);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        Assert.IsFalse(trip.Days[0].GetBlock(BlockType.Morning).Activities[0].OvertimeAlert,
            "Normally-placed activity should NOT have OvertimeAlert");
    }

    [TestMethod]
    public void Place_WithOvertimeFlagOn_TargetBlockHasActivities_ForcePlacesInEmptyAdjacentBlock()
    {
        // Morning has 1 activity (not full, but NOT empty).
        // Pinned must-see for Morning with overtime should NOT share the block;
        // it should overflow to Afternoon (empty) via force-placement.
        var mustSee = new MustSee(1, "Must-See", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        typeof(Trip).GetProperty("Preferences")!.SetValue(trip,
            new TripPreferences(allowMustSeeOvertime: true));
        var loc = new PlaceLocation(40.4168, -3.7038);

        // Put one normal activity in Morning so it's no longer empty
        trip.Days[0].GetBlock(BlockType.Morning).AddActivity(new ActivityNode(1, "Existing", 1, 60, location: loc));

        // Oversized must-see that won't fit duration in Morning anyway (220 > 210)
        var place = CreatePlace(2, "Oversized Place", 40.4190, -3.7060, duration: 220);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result, "Should force-place in adjacent empty block");
        Assert.AreEqual(1, trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count,
            "Force-placed activity should be in Afternoon because Morning is not empty");
        Assert.IsTrue(trip.Days[0].GetBlock(BlockType.Afternoon).Activities[0].OvertimeAlert,
            "Force-placed activity should have OvertimeAlert=true");
    }

    [TestMethod]
    public void Place_WithOvertimeFlagOn_NoEmptyBlock_ReturnsFalse()
    {
        // Every block has at least one activity — no empty block for exclusive overtime.
        var mustSee = new MustSee(1, "Must-See", Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        typeof(Trip).GetProperty("Preferences")!.SetValue(trip,
            new TripPreferences(allowMustSeeOvertime: true));
        var loc = new PlaceLocation(40.4168, -3.7038);

        trip.Days[0].GetBlock(BlockType.Morning).AddActivity(new ActivityNode(1, "A", 1, 60, location: loc));
        trip.Days[0].GetBlock(BlockType.Afternoon).AddActivity(new ActivityNode(2, "B", 1, 60, location: loc));
        trip.Days[0].GetBlock(BlockType.Evening).AddActivity(new ActivityNode(3, "C", 1, 50, location: loc));

        var place = CreatePlace(4, "Oversized Place", 40.4190, -3.7060, duration: 220);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result, "Should return false when no empty block is available for overtime");
    }

    [TestMethod]
    public void Place_AllBlocksFullOnTargetDay_ReturnsFalse()
    {
        var mustSee = new MustSee(1, "Must-See", Priority.High, pinnedDayIndex: 0);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);

        var loc = new PlaceLocation(40.4168, -3.7038);

        // Fill all blocks to max with activities that fit within block constraints.
        // Evening: max 2 visits × 50 min = 100 ≤ 105 max duration ✓
        // Morning/Afternoon: max 3 visits × 60 min each fits within their limits
        var morning = trip.Days[0].GetBlock(BlockType.Morning);
        morning.AddActivity(new ActivityNode(1, "A", 1, 60, location: loc));
        morning.AddActivity(new ActivityNode(2, "B", 2, 60, location: loc));
        morning.AddActivity(new ActivityNode(3, "C", 3, 60, location: loc));

        var afternoon = trip.Days[0].GetBlock(BlockType.Afternoon);
        afternoon.AddActivity(new ActivityNode(4, "D", 1, 60, location: loc));
        afternoon.AddActivity(new ActivityNode(5, "E", 2, 60, location: loc));
        afternoon.AddActivity(new ActivityNode(6, "F", 3, 60, location: loc));

        var evening = trip.Days[0].GetBlock(BlockType.Evening);
        evening.AddActivity(new ActivityNode(7, "G", 1, 50, location: loc));
        evening.AddActivity(new ActivityNode(8, "H", 2, 50, location: loc));

        var place = CreatePlace(9, "No Room", 40.4200, -3.7080);
        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result, "Should return false when all blocks are full");
    }
}
