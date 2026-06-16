using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class TravelersTests
{
    [TestMethod]
    public void Constructor_Default_CreatesSingleAdult()
    {
        var travelers = new Travelers(1);

        Assert.AreEqual(1, travelers.Adults);
        Assert.AreEqual(0, travelers.Children);
        Assert.AreEqual(0, travelers.Infants);
        Assert.AreEqual(1, travelers.Total);
    }

    [TestMethod]
    public void Constructor_WithChildrenAndInfants_SetsCorrectly()
    {
        var travelers = new Travelers(2, 1, 1);

        Assert.AreEqual(2, travelers.Adults);
        Assert.AreEqual(1, travelers.Children);
        Assert.AreEqual(1, travelers.Infants);
        Assert.AreEqual(4, travelers.Total);
    }

    [TestMethod]
    public void Constructor_AdultsBelowMinimum_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Travelers(0));
    }

    [TestMethod]
    public void Constructor_NegativeChildren_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Travelers(1, -1));
    }

    [TestMethod]
    public void Constructor_NegativeInfants_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Travelers(1, 0, -1));
    }

    [TestMethod]
    public void Constructor_TotalExceedsMax_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Travelers(11));
    }

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new Travelers(2, 1, 1);
        var b = new Travelers(2, 1, 1);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = new Travelers(2, 1, 0);
        var b = new Travelers(2, 2, 0);

        Assert.AreNotEqual(a, b);
    }
}
