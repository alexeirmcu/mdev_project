using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class TripTests
{
    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip() => new()
    {
        TripId = Guid.NewGuid(),
        TripCode = "MAD-2026-TEST",
        CityId = 1L,
        StartDate = FutureStartDate,
        EndDate = FutureStartDate.AddDays(2),
        BaseHotel = new Location("Hotel", 0, 0),
        OwnerUserId = "user-42",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [TestMethod]
    public void DefaultStartTime_Is09_00()
    {
        var trip = CreateTrip();
        Assert.AreEqual(new TimeOnly(9, 0), trip.DefaultStartTime);
    }

    [TestMethod]
    public void GenerateDaysFrom_CreatesCorrectNumberOfDayPlans()
    {
        var trip = CreateTrip(); // June 1 to June 3 = 3 days
        trip.GenerateDaysFrom(trip.StartDate);
        Assert.AreEqual(3, trip.Days.Count);
        Assert.AreEqual(trip.StartDate, trip.Days[0].Date);
        Assert.AreEqual(trip.StartDate.AddDays(1), trip.Days[1].Date);
        Assert.AreEqual(trip.StartDate.AddDays(2), trip.Days[2].Date);
    }

    [TestMethod]
    public void GenerateDaysFrom_ClearsExistingDays()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate);
        Assert.AreEqual(3, trip.Days.Count);

        // Call GenerateDaysFrom again — must clear and regenerate
        trip.GenerateDaysFrom(trip.StartDate);
        Assert.AreEqual(3, trip.Days.Count);
    }

    [TestMethod]
    public void GenerateDaysFrom_SetsCorrectBlockTypes()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate);
        var day = trip.Days[0];
        Assert.AreEqual(BlockType.Morning, day.GetBlock(BlockType.Morning).BlockType);
        Assert.AreEqual(BlockType.Afternoon, day.GetBlock(BlockType.Afternoon).BlockType);
        Assert.AreEqual(BlockType.Evening, day.GetBlock(BlockType.Evening).BlockType);
    }

    [TestMethod]
    public void GenerateDaysFrom_SetsDayIndexSequentially()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate);
        for (int i = 0; i < trip.Days.Count; i++)
            Assert.AreEqual(i, trip.Days[i].DayIndex);
    }

    [TestMethod]
    public void AddMustSee_AddsToList()
    {
        var trip = CreateTrip();
        var mustSee = new MustSee(42L, "Place", Priority.High);

        trip.AddMustSee(mustSee);

        Assert.HasCount(1, trip.OriginalMustSees);
        Assert.AreEqual(42L, trip.OriginalMustSees[0].PlaceId);
    }

    [TestMethod]
    public void AddMustSee_DuplicatePlaceId_ThrowsDomainException()
    {
        var trip = CreateTrip();
        var mustSee = new MustSee(42L, "Place", Priority.High);

        trip.AddMustSee(mustSee);

        Assert.ThrowsExactly<SmartTripDomainException>(() => trip.AddMustSee(mustSee));
    }

    [TestMethod]
    public void RemoveMustSee_Existing_ReturnsTrueAndRemoves()
    {
        var trip = CreateTrip();
        trip.AddMustSee(new MustSee(42L, "Place 1", Priority.High));
        trip.AddMustSee(new MustSee(43L, "Place 2", Priority.Medium));

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
    public void ClearDaysAndReset_ClearsDays()
    {
        var trip = CreateTrip();
        trip.GenerateDaysFrom(trip.StartDate);
        Assert.AreEqual(3, trip.Days.Count);

        trip.ClearDaysAndReset();

        Assert.AreEqual(0, trip.Days.Count);
        Assert.IsFalse(trip.Days.Any());
    }

    [TestMethod]
    public void UpdateDates_WithValidRange_SetsDates()
    {
        var trip = CreateTrip();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var end = start.AddDays(4);

        trip.UpdateDates(start, end);

        Assert.AreEqual(start, trip.StartDate);
        Assert.AreEqual(end, trip.EndDate);
    }

    [TestMethod]
    public void UpdateDates_StartAfterEnd_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip();

        Assert.ThrowsExactly<BusinessRuleException>(() =>
            trip.UpdateDates(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6))));
    }

    [TestMethod]
    public void UpdateDates_ExceedsMaxDuration_ThrowsBusinessRuleException()
    {
        var trip = CreateTrip();

        Assert.ThrowsExactly<BusinessRuleException>(() =>
            trip.UpdateDates(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(24))));
    }

    [TestMethod]
    public void UpdateBaseHotel_SetsHotel()
    {
        var trip = CreateTrip();
        var hotel = new Location("New Hotel", 10.0, 20.0);

        trip.UpdateBaseHotel(hotel);

        Assert.AreEqual("New Hotel", trip.BaseHotel!.Name);
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
