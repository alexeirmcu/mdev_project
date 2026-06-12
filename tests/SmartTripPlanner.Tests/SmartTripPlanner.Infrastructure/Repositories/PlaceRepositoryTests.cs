using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.Repositories;

namespace SmartTripPlanner.Tests.Infrastructure.Repositories;

[TestClass]
public sealed class PlaceRepositoryTests
{
    private static PlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlannerDbContext(options);
    }

    [TestMethod]
    public async Task SearchAsync_WithMatchingQuery_ReturnsResults()
    {
        using var db = CreateDbContext();
        db.Places.Add(new Place("f1", "Museo del Prado", "madrid-es", new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Museo Reina Sofia", "madrid-es", new PlaceLocation(40.4089, -3.6944)));
        db.Places.Add(new Place("f3", "Louvre Museum", "paris-fr", new PlaceLocation(48.8606, 2.3376)));
        await db.SaveChangesAsync();

        var repo = new PlaceRepository(db);
        var results = await repo.SearchAsync("Museo", "madrid-es");

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_WithNonMatchingQuery_ReturnsEmpty()
    {
        using var db = CreateDbContext();
        db.Places.Add(new Place("f1", "Museo del Prado", "madrid-es", new PlaceLocation(40.4168, -3.7038)));
        await db.SaveChangesAsync();

        var repo = new PlaceRepository(db);
        var results = await repo.SearchAsync("Zoo", "madrid-es");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_FiltersByCityId()
    {
        using var db = CreateDbContext();
        db.Places.Add(new Place("f1", "Museo del Prado", "madrid-es", new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Louvre Museum", "paris-fr", new PlaceLocation(48.8606, 2.3376)));
        await db.SaveChangesAsync();

        var repo = new PlaceRepository(db);
        var results = await repo.SearchAsync("Louvre", "paris-fr");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Louvre Museum", results.Single().Name);
    }

    [TestMethod]
    public async Task SearchAsync_RespectsMaxResults()
    {
        using var db = CreateDbContext();
        for (int i = 0; i < 5; i++)
            db.Places.Add(new Place($"f{i}", $"Place {i}", "city", new PlaceLocation(0, 0)));
        await db.SaveChangesAsync();

        var repo = new PlaceRepository(db);
        var results = await repo.SearchAsync("Place", "city", maxResults: 3);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task GetByPlaceIdAsync_WithExistingId_ReturnsPlace()
    {
        using var db = CreateDbContext();
        db.Places.Add(new Place("fsq123", "Museo del Prado", "madrid-es", new PlaceLocation(40.4168, -3.7038)));
        await db.SaveChangesAsync();

        var repo = new PlaceRepository(db);
        var place = await repo.GetByPlaceIdAsync("fsq123");

        Assert.IsNotNull(place);
        Assert.AreEqual("Museo del Prado", place.Name);
    }

    [TestMethod]
    public async Task GetByPlaceIdAsync_WithNonExistingId_ReturnsNull()
    {
        using var db = CreateDbContext();
        var repo = new PlaceRepository(db);
        var place = await repo.GetByPlaceIdAsync("nonexistent");

        Assert.IsNull(place);
    }

    [TestMethod]
    public async Task SavePlace_PreservesAllProperties()
    {
        using var db = CreateDbContext();
        var openingHours = new List<OpeningHoursWindow>
        {
            new(DayOfWeek.Monday, 540, 1260),
            new(DayOfWeek.Tuesday, 540, 1260)
        };
        var location = new PlaceLocation(40.4168, -3.7038);
        var place = new Place("fsq123", "Museo del Prado", "madrid-es", location);
        place.OpeningHours.AddRange(openingHours);

        db.Places.Add(place);
        await db.SaveChangesAsync();

        var saved = await db.Places
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.PlaceId == "fsq123");

        Assert.IsNotNull(saved);
        Assert.AreEqual("fsq123", saved.PlaceId);
        Assert.AreEqual("Museo del Prado", saved.Name);
        Assert.AreEqual("madrid-es", saved.CityId);
        Assert.AreEqual(40.4168, saved.Location.Latitude);
        Assert.AreEqual(-3.7038, saved.Location.Longitude);
        Assert.AreEqual(60, saved.TypicalDurationMinutes);
        Assert.IsFalse(saved.IsIndoor);
        Assert.IsTrue(saved.IsFamilyFriendly);
        Assert.AreEqual(2, saved.OpeningHours.Count);
    }

    [TestMethod]
    public async Task AddRangeAsync_AddsMultiplePlaces()
    {
        using var db = CreateDbContext();
        var repo = new PlaceRepository(db);

        var places = new List<Place>
        {
            new("fsq_a", "Place A", "madrid-es", new PlaceLocation(40.0, -3.0)),
            new("fsq_b", "Place B", "madrid-es", new PlaceLocation(41.0, -4.0)),
        };

        await repo.AddRangeAsync(places);
        await repo.UnitOfWork.SaveChangesAsync();

        var saved = await db.Places.ToListAsync();
        Assert.AreEqual(2, saved.Count);
    }
}
