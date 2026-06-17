using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class TripTests
{
    private static Trip CreateTrip() => new()
    {
        CityId = "par",
        StartDate = new DateOnly(2026, 6, 1),
        EndDate = new DateOnly(2026, 6, 3),
        BaseHotel = new Location("Hotel", 0, 0)
    };

    private static Place CreatePlace(long id) => new($"fsq-{id}", $"Place {id}", 1L,
        new PlaceLocation(0, 0));

    [TestMethod]
    public void SelectPlace_AddsToList()
    {
        var trip = CreateTrip();
        var place = CreatePlace(1);
        trip.SelectPlace(place);
        Assert.HasCount(1, trip.SelectedPlaces);
    }

    [TestMethod]
    public void UnselectPlace_RemovesFromList()
    {
        var trip = CreateTrip();
        var place1 = CreatePlace(1);
        var place2 = CreatePlace(2);
        trip.SelectPlace(place1);
        trip.SelectPlace(place2);
        bool removed = trip.UnselectPlace(place1.Id);
        Assert.IsTrue(removed);
        Assert.HasCount(1, trip.SelectedPlaces);
    }

    [TestMethod]
    public void UnselectPlace_ReturnsFalseWhenNotFound()
    {
        var trip = CreateTrip();
        bool removed = trip.UnselectPlace(999);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void DefaultStartTime_Is09_00()
    {
        var trip = CreateTrip();
        Assert.AreEqual(new TimeOnly(9, 0), trip.DefaultStartTime);
    }

    [TestMethod]
    public void GenerateDays_CreatesCorrectNumberOfDayPlans()
    {
        var trip = CreateTrip(); // June 1 to June 3 = 3 days
        trip.GenerateDays();
        Assert.AreEqual(3, trip.Days.Count);
        Assert.AreEqual(new DateOnly(2026, 6, 1), trip.Days[0].Date);
        Assert.AreEqual(new DateOnly(2026, 6, 2), trip.Days[1].Date);
        Assert.AreEqual(new DateOnly(2026, 6, 3), trip.Days[2].Date);
    }

    [TestMethod]
    public void GenerateDays_ClearsExistingDays()
    {
        var trip = CreateTrip();
        trip.GenerateDays();
        Assert.AreEqual(3, trip.Days.Count);

        // Call GenerateDays again — must clear and regenerate
        trip.GenerateDays();
        Assert.AreEqual(3, trip.Days.Count);
    }

    [TestMethod]
    public void GenerateDays_SetsCorrectBlockTypes()
    {
        var trip = CreateTrip();
        trip.GenerateDays();
        var day = trip.Days[0];
        Assert.AreEqual(BlockType.Morning, day.Morning.BlockType);
        Assert.AreEqual(BlockType.Afternoon, day.Afternoon.BlockType);
        Assert.AreEqual(BlockType.Evening, day.Evening.BlockType);
    }

    [TestMethod]
    public void GenerateDays_SetsDayIndexSequentially()
    {
        var trip = CreateTrip();
        trip.GenerateDays();
        for (int i = 0; i < trip.Days.Count; i++)
            Assert.AreEqual(i, trip.Days[i].DayIndex);
    }
}
