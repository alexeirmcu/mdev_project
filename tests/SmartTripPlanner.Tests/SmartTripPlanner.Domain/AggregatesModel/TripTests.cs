using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class TripTests
{
    private static Trip CreateTrip() => new()
    {
        TripId = Guid.NewGuid(),
        TripCode = "MAD-2026-TEST",
        CityId = 1L,
        StartDate = new DateOnly(2026, 6, 1),
        EndDate = new DateOnly(2026, 6, 3),
        BaseHotel = new Location("Hotel", 0, 0),
        CreatedAt = DateTimeOffset.UtcNow
    };

    [TestMethod]
    public void DefaultStartTime_Is09_00()
    {
        var trip = CreateTrip();
        Assert.AreEqual(new TimeOnly(9, 0), trip.DefaultStartTime);
    }

    [TestMethod]
    public void AddMustSee_AddsToList()
    {
        var trip = CreateTrip();
        var mustSee = new MustSee(42L, Priority.HIGH);

        trip.AddMustSee(mustSee);

        Assert.HasCount(1, trip.OriginalMustSees);
        Assert.AreEqual(42L, trip.OriginalMustSees[0].PlaceId);
    }

    [TestMethod]
    public void AddMustSee_DuplicatePlaceId_ThrowsDomainException()
    {
        var trip = CreateTrip();
        var mustSee = new MustSee(42L, Priority.HIGH);

        trip.AddMustSee(mustSee);

        Assert.ThrowsExactly<SmartTripDomainException>(() => trip.AddMustSee(mustSee));
    }

    [TestMethod]
    public void RemoveMustSee_Existing_ReturnsTrueAndRemoves()
    {
        var trip = CreateTrip();
        trip.AddMustSee(new MustSee(42L, Priority.HIGH));
        trip.AddMustSee(new MustSee(43L, Priority.MEDIUM));

        bool removed = trip.RemoveMustSee(42L);

        Assert.IsTrue(removed);
        Assert.HasCount(1, trip.OriginalMustSees);
        Assert.AreEqual(43L, trip.OriginalMustSees[0].PlaceId);
    }

    [TestMethod]
    public void RemoveMustSee_NonExisting_ReturnsFalse()
    {
        var trip = CreateTrip();
        bool removed = trip.RemoveMustSee(999L);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void Status_DefaultIsCreated()
    {
        var trip = CreateTrip();
        Assert.AreEqual(TripStatus.CREATED, trip.Status);
    }

    [TestMethod]
    public void UpdateStatus_ChangesStatus()
    {
        var trip = CreateTrip();
        trip.UpdateStatus(TripStatus.COMPLETED);
        Assert.AreEqual(TripStatus.COMPLETED, trip.Status);
    }

    [TestMethod]
    public void TripId_IsNotEmptyGuid()
    {
        var trip = CreateTrip();
        Assert.AreNotEqual(Guid.Empty, trip.TripId);
    }

    [TestMethod]
    public void TripCode_IsNotNull()
    {
        var trip = CreateTrip();
        Assert.AreEqual("MAD-2026-TEST", trip.TripCode);
    }

    [TestMethod]
    public void CityId_IsLong()
    {
        var trip = CreateTrip();
        Assert.AreEqual(1L, trip.CityId);
        Assert.IsInstanceOfType(trip.CityId, typeof(long));
    }
}
