using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Mapping;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Foursquare;

[TestClass]
public sealed class FoursquareCategoryHeuristicsTests
{
    [TestMethod]
    public void Map_MuseumCategory_Returns120MinAndIndoor()
    {
        var categories = new List<FoursquareCategory>
        {
            new() { Id = "10000", Name = "Museum" }
        };

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(120, duration);
        Assert.IsTrue(indoor);
        Assert.IsTrue(familyFriendly);
    }

    [TestMethod]
    public void Map_HistoricSite_Returns60Min()
    {
        var categories = new List<FoursquareCategory>
        {
            new() { Id = "10024", Name = "Historic Site" }
        };

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(60, duration);
        Assert.IsTrue(indoor);
        Assert.IsTrue(familyFriendly);
    }

    [TestMethod]
    public void Map_Restaurant_Returns90Min()
    {
        var categories = new List<FoursquareCategory>
        {
            new() { Id = "13003", Name = "Restaurant" }
        };

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(90, duration);
        Assert.IsTrue(indoor);
        Assert.IsTrue(familyFriendly);
    }

    [TestMethod]
    public void Map_NightclubCategory_ReturnsNotFamilyFriendly()
    {
        var categories = new List<FoursquareCategory>
        {
            new() { Id = "10008", Name = "Nightclub" }
        };

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(60, duration);
        Assert.IsTrue(indoor);
        Assert.IsFalse(familyFriendly);
    }

    [TestMethod]
    public void Map_EmptyCategories_ReturnsDefaults()
    {
        var categories = new List<FoursquareCategory>();

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(60, duration);
        Assert.IsTrue(indoor);
        Assert.IsTrue(familyFriendly);
    }

    [TestMethod]
    public void Map_UnknownCategory_ReturnsDefaults()
    {
        var categories = new List<FoursquareCategory>
        {
            new() { Id = "99999", Name = "Unknown" }
        };

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(60, duration);
        Assert.IsTrue(indoor);
        Assert.IsTrue(familyFriendly);
    }

    [TestMethod]
    public void Map_MuseumRestaurant_FirstCategoryWins()
    {
        var categories = new List<FoursquareCategory>
        {
            new() { Id = "10000", Name = "Museum" },
            new() { Id = "13003", Name = "Restaurant" }
        };

        var (duration, indoor, familyFriendly) = FoursquareCategoryHeuristics.Map(categories);

        Assert.AreEqual(120, duration);
        Assert.IsTrue(indoor);
        Assert.IsTrue(familyFriendly);
    }
}
