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

    [TestMethod]
    public void AddSelectedAttraction_AddsToList()
    {
        var trip = CreateTrip();
        trip.AddSelectedAttraction("place1", "Eiffel Tower");
        Assert.HasCount(1, trip.SelectedAttractions);
    }

    [TestMethod]
    public void RemoveSelectedAttraction_RemovesFromList()
    {
        var trip = CreateTrip();
        trip.AddSelectedAttraction("place1", "Eiffel Tower");
        trip.AddSelectedAttraction("place2", "Louvre");
        bool removed = trip.RemoveSelectedAttraction("place1");
        Assert.IsTrue(removed);
        Assert.HasCount(1, trip.SelectedAttractions);
    }

    [TestMethod]
    public void RemoveSelectedAttraction_ReturnsFalseWhenNotFound()
    {
        var trip = CreateTrip();
        bool removed = trip.RemoveSelectedAttraction("nonexistent");
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void DefaultStartTime_Is09_00()
    {
        var trip = CreateTrip();
        Assert.AreEqual(new TimeOnly(9, 0), trip.DefaultStartTime);
    }
}
