using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Services;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class UnpinnedMustSeePlacerTests
{
    private readonly UnpinnedMustSeePlacer _placer = new();

    private static Place CreatePlace(long id, string name, double lat, double lng, int duration = 60)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng), duration, false, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));

        return place;
    }

    private static Place CreatePlaceWithHours(long id, string name, double lat, double lng,
        DayOfWeek[]? closedDays = null, int duration = 60)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng), duration, false, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        var closed = closedDays?.ToHashSet() ?? new HashSet<DayOfWeek>();
        foreach (var day in Enum.GetValues<DayOfWeek>().Cast<DayOfWeek>())
        {
            if (!closed.Contains(day))
                place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));
        }

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

        trip.GenerateDays();

        return trip;
    }

    [TestMethod]
    public void Place_PlacesInFirstAvailableDay()
    {
        var mustSee = new MustSee(1, Priority.High);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 3);
        var place = CreatePlace(1, "Place", 40.4168, -3.7038);

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        // Should be placed on day 0 by default (open day with free slots)
        var total = trip.Days.Sum(d =>
            d.Morning.Activities.Count + d.Afternoon.Activities.Count + d.Evening.Activities.Count);
        Assert.AreEqual(1, total);
    }

    [TestMethod]
    public void Place_PrefersOpenDaysOverClosedDays()
    {
        // Trip starts Wednesday. Place closed on Wednesday.
        var mustSee = new MustSee(1, Priority.High);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 3);
        var place = CreatePlaceWithHours(1, "Closed Wed", 40.4168, -3.7038,
            closedDays: new[] { DayOfWeek.Wednesday });

        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        // Should NOT be on day 0 (Wednesday)
        Assert.AreEqual(0, trip.Days[0].Morning.Activities.Count);
    }

    [TestMethod]
    public void Place_AllDaysFull_ReturnsFalse()
    {
        var mustSee = new MustSee(1, Priority.High);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 1);
        var loc = new PlaceLocation(40.4168, -3.7038);

        // Fill all blocks to max with activities that fit within block constraints.
        // Evening: max 2 visits × 50 min = 100 ≤ 105 max duration ✓
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

        var place = CreatePlace(9, "Overflow", 40.4200, -3.7080);
        var result = _placer.Place(trip, mustSee, place);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Place_MultipleDays_PicksDayWithMostFreeSlots()
    {
        var mustSee = new MustSee(1, Priority.High);
        var trip = CreateTrip(new[] { mustSee }, dayCount: 3);

        // Fill day 0 morning partially — reduces free slots so day 0 is less attractive
        var loc = new PlaceLocation(40.4168, -3.7038);
        trip.Days[0].Morning.AddActivity(new ActivityNode(1, "Existing", 1, 60, location: loc));
        // day 0 now has: 2 free morning + 3 afternoon + 2 evening = 7 free slots
        // day 1 and day 2: 8 free slots each (all empty)

        var place = CreatePlace(2, "New Place", 40.4170, -3.7040);
        var result = _placer.Place(trip, mustSee, place);

        Assert.IsTrue(result);
        // 1 existing (day 0) + 1 new (preferably day 1 or 2) = 2 total
        var totalActivities = trip.Days.Sum(d =>
            d.Morning.Activities.Count + d.Afternoon.Activities.Count + d.Evening.Activities.Count);
        Assert.AreEqual(2, totalActivities, "Should have 1 existing + 1 newly placed activity");

        // New activity should be on day 1 or 2 (more free slots), not day 0
        var day1or2Morning = trip.Days[1].Morning.Activities.Count + trip.Days[2].Morning.Activities.Count;
        Assert.AreEqual(1, day1or2Morning, "New activity should be placed on day 1 or 2 (most free slots)");
    }
}
