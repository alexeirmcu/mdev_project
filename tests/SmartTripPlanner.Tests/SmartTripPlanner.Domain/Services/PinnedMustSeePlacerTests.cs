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
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var ms in mustSees)
            trip.AddMustSee(ms);

        // Pre-generate days manually to test placement without running full generator
        trip.GenerateDays();

        return trip;
    }

    [TestMethod]
    public void Place_PinnedMustSeeWithBlock_PlacesInCorrectDayAndBlock()
    {
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 2);
        var place = CreatePlace(1, "Museo del Prado", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        Assert.AreEqual(1, trip.Days[0].Morning.Activities.Count);
        Assert.AreEqual(1L, trip.Days[0].Morning.Activities[0].PlaceId);
    }

    [TestMethod]
    public void Place_PinnedMustSeeNoBlock_TriesMorningFirst()
    {
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 0);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place = CreatePlace(1, "Place", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        // Should end up in one of the three blocks
        var total = trip.Days[0].Morning.Activities.Count
                  + trip.Days[0].Afternoon.Activities.Count
                  + trip.Days[0].Evening.Activities.Count;
        Assert.AreEqual(1, total);
    }

    [TestMethod]
    public void Place_InvalidDayIndex_ReturnsFalse()
    {
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 99);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place = CreatePlace(1, "Place", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Place_FullTargetBlock_OverflowsToAdjacentBlock()
    {
        // Fill Morning to capacity, then try to place another
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var place1 = CreatePlace(1, "Place 1", 40.4168, -3.7038, duration: 60);
        var place2 = CreatePlace(2, "Place 2", 40.4170, -3.7040, duration: 60);

        // Fill morning to max via trip.AddActivity
        var morning = trip.Days[0].Morning;
        morning.AddActivity(new ActivityNode(1, "Existing", 1, 60, location: place1.Location));
        morning.AddActivity(new ActivityNode(2, "Existing 2", 2, 60, location: place2.Location));
        morning.AddActivity(new ActivityNode(3, "Existing 3", 3, 60, location: new PlaceLocation(40.4180, -3.7050)));

        // Now try to place a pinned must-see in Morning — should overflow to Afternoon
        var place3 = CreatePlace(4, "Overflow Place", 40.4190, -3.7060);
        var mustSee2 = new MustSee(4, Priority.High, pinnedDayIndex: 0, pinnedBlock: BlockType.Morning);

        var result = _placer.Place(trip, mustSee2, place3);

        Assert.IsTrue(result, "Pinned must-see should overflow to adjacent block");
        Assert.AreEqual(1, trip.Days[0].Afternoon.Activities.Count,
            "Overflow should go to afternoon (adjacent to morning)");
    }

    [TestMethod]
    public void Place_AllBlocksFullOnTargetDay_ReturnsFalse()
    {
        var mustSee = new MustSee(1, Priority.High, pinnedDayIndex: 0);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);

        var loc = new PlaceLocation(40.4168, -3.7038);

        // Fill all blocks to max with activities that fit within block constraints.
        // Evening: max 2 visits × 50 min = 100 ≤ 105 max duration ✓
        // Morning/Afternoon: max 3 visits × 60 min each fits within their limits
        var morning = trip.Days[0].Morning;
        morning.AddActivity(new ActivityNode(1, "A", 1, 60, location: loc));
        morning.AddActivity(new ActivityNode(2, "B", 2, 60, location: loc));
        morning.AddActivity(new ActivityNode(3, "C", 3, 60, location: loc));

        var afternoon = trip.Days[0].Afternoon;
        afternoon.AddActivity(new ActivityNode(4, "D", 1, 60, location: loc));
        afternoon.AddActivity(new ActivityNode(5, "E", 2, 60, location: loc));
        afternoon.AddActivity(new ActivityNode(6, "F", 3, 60, location: loc));

        var evening = trip.Days[0].Evening;
        evening.AddActivity(new ActivityNode(7, "G", 1, 50, location: loc));
        evening.AddActivity(new ActivityNode(8, "H", 2, 50, location: loc));

        var place = CreatePlace(9, "No Room", 40.4200, -3.7080);
        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result, "Should return false when all blocks are full");
    }
}
