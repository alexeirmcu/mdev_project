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

    private static PlaceRepository CreateRepository(PlannerDbContext db)
    {
        return new PlaceRepository(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaceRepository>.Instance);
    }

    [TestMethod]
    public async Task SearchAsync_WithMatchingQuery_ReturnsResults()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        var paris = new City("paris-fr", "Paris", true);
        db.Cities.AddRange(madrid, paris);
        await db.SaveChangesAsync();

        db.Places.Add(new Place("f1", "Museo del Prado", madrid.Id, new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Museo Reina Sofia", madrid.Id, new PlaceLocation(40.4089, -3.6944)));
        db.Places.Add(new Place("f3", "Louvre Museum", paris.Id, new PlaceLocation(48.8606, 2.3376)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Museo", "madrid-es");

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_WithNonMatchingQuery_ReturnsEmpty()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        db.Places.Add(new Place("f1", "Museo del Prado", madrid.Id, new PlaceLocation(40.4168, -3.7038)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Zoo", "madrid-es");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_FiltersByCityCode()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        var paris = new City("paris-fr", "Paris", true);
        db.Cities.AddRange(madrid, paris);
        await db.SaveChangesAsync();

        db.Places.Add(new Place("f1", "Museo del Prado", madrid.Id, new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Louvre Museum", paris.Id, new PlaceLocation(48.8606, 2.3376)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Louvre", "paris-fr");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Louvre Museum", results.Single().Name);
    }

    [TestMethod]
    public async Task SearchAsync_RespectsMaxResults()
    {
        using var db = CreateDbContext();
        var city = new City("city", "City", true);
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
            db.Places.Add(new Place($"f{i}", $"Place {i}", city.Id, new PlaceLocation(0, 0)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Place", "city", maxResults: 3);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task GetByProviderReferenceIdAsync_WithExistingId_ReturnsPlace()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        db.Places.Add(new Place("fsq123", "Museo del Prado", madrid.Id, new PlaceLocation(40.4168, -3.7038)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var place = await repo.GetByProviderReferenceIdAsync("fsq123");

        Assert.IsNotNull(place);
        Assert.AreEqual("Museo del Prado", place.Name);
    }

    [TestMethod]
    public async Task GetByProviderReferenceIdAsync_WithNonExistingId_ReturnsNull()
    {
        using var db = CreateDbContext();
        var repo = CreateRepository(db);
        var place = await repo.GetByProviderReferenceIdAsync("nonexistent");

        Assert.IsNull(place);
    }

    [TestMethod]
    public async Task SavePlace_PreservesAllProperties()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var openingHours = new List<OpeningHoursWindow>
        {
            new(DayOfWeek.Monday, 540, 1260),
            new(DayOfWeek.Tuesday, 540, 1260)
        };
        var location = new PlaceLocation(40.4168, -3.7038);
        var place = new Place("fsq123", "Museo del Prado", madrid.Id, location);
        place.OpeningHours.AddRange(openingHours);

        db.Places.Add(place);
        await db.SaveChangesAsync();

        var saved = await db.Places
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.ProviderReferenceId == "fsq123");

        Assert.IsNotNull(saved);
        Assert.AreEqual("fsq123", saved.ProviderReferenceId);
        Assert.AreEqual("Museo del Prado", saved.Name);
        Assert.AreEqual(madrid.Id, saved.CityId);
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
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);

        var places = new List<Place>
        {
            new("fsq_a", "Place A", madrid.Id, new PlaceLocation(40.0, -3.0)),
            new("fsq_b", "Place B", madrid.Id, new PlaceLocation(41.0, -4.0)),
        };

        await repo.AddRangeAsync(places);
        await repo.UnitOfWork.SaveChangesAsync();

        var saved = await db.Places.ToListAsync();
        Assert.AreEqual(2, saved.Count);
    }

    [TestMethod]
    public async Task SearchAsync_WithAttributeValueMatch_ReturnsPlace()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_gran_palace", "Gran Palace", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Hotel", "madrid-es");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Gran Palace", results[0].Name);
    }

    [TestMethod]
    public async Task SearchAsync_NameMatchStillWorks_WhenAttributesMatch()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_hotel_california", "Hotel California", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Lodging"));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Hotel", "madrid-es");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Hotel California", results[0].Name);
    }

    [TestMethod]
    public async Task SearchAsync_ByChainAttribute_ReturnsPlace()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_mcd_123", "McDonald's Centro", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        place.AddAttribute(new PlaceAttribute("foursquare", "chain", "McDonald's"));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("McDonald's", "madrid-es");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("McDonald's Centro", results[0].Name);
    }

    [TestMethod]
    public async Task SearchAsync_WithAttributes_DoesNotReturnPlaceFromDifferentCity()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        var barcelona = new City("barcelona-es", "Barcelona", true);
        db.Cities.AddRange(madrid, barcelona);
        await db.SaveChangesAsync();

        var madridPlace = new Place("fsq_mad_1", "Gran Palace", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        madridPlace.AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"));
        db.Places.Add(madridPlace);

        var bcnPlace = new Place("fsq_bcn_1", "Hotel Arts", barcelona.Id,
            new PlaceLocation(41.3874, 2.1686));
        db.Places.Add(bcnPlace);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("Hotel", "madrid-es");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Gran Palace", results[0].Name);
    }

    [TestMethod]
    public async Task SearchAsync_LowercaseQuery_MatchesAttributeValue()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_gran_palace", "Gran Palace", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.SearchAsync("hotel", "madrid-es");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Gran Palace", results[0].Name);
    }

    [TestMethod]
    public async Task SavePlace_PreservesAttributes()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_palace", "Gran Palace", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"));
        place.AddAttribute(new PlaceAttribute("foursquare", "chain", "Iberostar"));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var saved = await repo.GetByProviderReferenceIdAsync("fsq_palace");

        Assert.IsNotNull(saved);
        Assert.AreEqual(2, saved.Attributes.Count);

        var category = saved.Attributes.First(a => a.Key == "category");
        Assert.AreEqual("Hotel", category.Value);

        var chain = saved.Attributes.First(a => a.Key == "chain");
        Assert.AreEqual("Iberostar", chain.Value);
    }

    [TestMethod]
    public async Task GetManyByCityIdAsync_ReturnsPlacesForCity()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        var paris = new City("paris-fr", "Paris", true);
        db.Cities.AddRange(madrid, paris);
        await db.SaveChangesAsync();

        db.Places.Add(new Place("f1", "Museo del Prado", madrid.Id, new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Louvre Museum", paris.Id, new PlaceLocation(48.8606, 2.3376)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.GetManyByCityIdAsync(madrid.Id);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Museo del Prado", results[0].Name);
    }

    [TestMethod]
    public async Task GetManyByCityIdAsync_ReturnsEmptyForUnknownCity()
    {
        using var db = CreateDbContext();
        var repo = CreateRepository(db);

        var results = await repo.GetManyByCityIdAsync(999);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task GetManyByCityIdAsync_ReturnsMultiplePlaces()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        db.Places.Add(new Place("f1", "Museo del Prado", madrid.Id, new PlaceLocation(40.4168, -3.7038)));
        db.Places.Add(new Place("f2", "Reina Sofia", madrid.Id, new PlaceLocation(40.4089, -3.6944)));
        db.Places.Add(new Place("f3", "Thyssen", madrid.Id, new PlaceLocation(40.4162, -3.6949)));
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.GetManyByCityIdAsync(madrid.Id);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task GetManyByCityIdAsync_IncludesOpeningHours()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_test", "Test Place", madrid.Id, new PlaceLocation(40.4168, -3.7038));
        place.OpeningHours.Add(new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var results = await repo.GetManyByCityIdAsync(madrid.Id);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(1, results[0].OpeningHours.Count);
        Assert.AreEqual(DayOfWeek.Monday, results[0].OpeningHours[0].DayOfWeek);
    }

    [TestMethod]
    public async Task SavePlace_PreservesAttributes_AsSeparateEntities()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place = new Place("fsq_palace", "Gran Palace", madrid.Id,
            new PlaceLocation(40.4168, -3.7038));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"));
        place.AddAttribute(new PlaceAttribute("foursquare", "chain", "Iberostar"));
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);

        // Verify attribute entities exist in PlaceAttributes table separately
        var attributeCount = await db.PlaceAttributes.CountAsync();
        Assert.AreEqual(2, attributeCount);

        // Verify join table rows exist
        var joinCount = await db.PlacePlaceAttributes.CountAsync();
        Assert.AreEqual(2, joinCount);

        // Verify place can be loaded with attributes through join table
        var saved = await repo.GetByProviderReferenceIdAsync("fsq_palace");
        Assert.IsNotNull(saved);
        Assert.AreEqual(2, saved.Attributes.Count);
    }

    [TestMethod]
    public async Task UpsertRangeAsync_FindOrCreate_SharedAttribute_NoDuplicateRows()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);

        // First place with attribute
        var place1 = new Place("fsq_a", "Place A", madrid.Id, new PlaceLocation(40.0, -3.0));
        place1.AddAttribute(new PlaceAttribute("foursquare", "category", "Museum"));
        await repo.UpsertRangeAsync(new[] { place1 });
        await repo.UnitOfWork.SaveChangesAsync();

        // Verify first place added and attribute created
        Assert.AreEqual(1, await db.PlaceAttributes.CountAsync());
        Assert.AreEqual(1, await db.PlacePlaceAttributes.CountAsync());

        // Second place with SAME attribute value — should reuse existing attribute
        var existingAttr = await db.PlaceAttributes.FirstAsync();
        var place2 = new Place("fsq_b", "Place B", madrid.Id, new PlaceLocation(41.0, -4.0));
        place2.AddAttribute(new PlaceAttribute("foursquare", "category", "Museum"));
        await repo.UpsertRangeAsync(new[] { place2 });
        await repo.UnitOfWork.SaveChangesAsync();

        // Only one PlaceAttribute row should exist
        Assert.AreEqual(1, await db.PlaceAttributes.CountAsync());

        // Two join table rows should exist
        Assert.AreEqual(2, await db.PlacePlaceAttributes.CountAsync());

        // Both places should have the attribute loaded
        var savedA = await db.Places.Include(p => p.Attributes).FirstAsync(p => p.ProviderReferenceId == "fsq_a");
        var savedB = await db.Places.Include(p => p.Attributes).FirstAsync(p => p.ProviderReferenceId == "fsq_b");
        Assert.AreEqual(1, savedA.Attributes.Count);
        Assert.AreEqual(1, savedB.Attributes.Count);
        Assert.AreSame(savedA.Attributes.First(), savedB.Attributes.First());
    }

    [TestMethod]
    public async Task UpsertRangeAsync_FindOrCreate_CreatesNewAttribute_WhenNotFound()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var repo = CreateRepository(db);

        var place = new Place("fsq_new", "New Place", madrid.Id, new PlaceLocation(40.0, -3.0));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "Aquarium"));
        await repo.UpsertRangeAsync(new[] { place });
        await repo.UnitOfWork.SaveChangesAsync();

        // New attribute should be created
        Assert.AreEqual(1, await db.PlaceAttributes.CountAsync());
        var attr = await db.PlaceAttributes.FirstAsync();
        Assert.AreEqual("Aquarium", attr.Value);
    }

    [TestMethod]
    public async Task GetDistinctInterestsByCityIdAsync_ReturnsDistinctValues()
    {
        using var db = CreateDbContext();
        var madrid = new City("madrid-es", "Madrid", true);
        db.Cities.Add(madrid);
        await db.SaveChangesAsync();

        var place1 = new Place("fsq_a", "Place A", madrid.Id, new PlaceLocation(40.0, -3.0));
        place1.AddAttribute(new PlaceAttribute("foursquare", "category", "Museum"));
        place1.AddAttribute(new PlaceAttribute("foursquare", "category", "History"));
        db.Places.Add(place1);

        var place2 = new Place("fsq_b", "Place B", madrid.Id, new PlaceLocation(41.0, -4.0));
        place2.AddAttribute(new PlaceAttribute("foursquare", "category", "Museum"));
        place2.AddAttribute(new PlaceAttribute("foursquare", "chain", "Food"));
        db.Places.Add(place2);

        await db.SaveChangesAsync();

        var repo = CreateRepository(db);
        var interests = await repo.GetDistinctInterestsByCityIdAsync(madrid.Id);

        Assert.AreEqual(3, interests.Count);
        CollectionAssert.Contains(interests.ToList(), "Museum");
        CollectionAssert.Contains(interests.ToList(), "History");
        CollectionAssert.Contains(interests.ToList(), "Food");
    }

    [TestMethod]
    public async Task GetDistinctInterestsByCityIdAsync_ReturnsEmpty_WhenNoPlaces()
    {
        using var db = CreateDbContext();
        var repo = CreateRepository(db);

        var interests = await repo.GetDistinctInterestsByCityIdAsync(999);

        Assert.AreEqual(0, interests.Count);
    }
}
