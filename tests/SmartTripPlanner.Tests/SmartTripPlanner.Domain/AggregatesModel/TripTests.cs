using SmartTripPlanner.Domain.AggregatesModel;

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
}
