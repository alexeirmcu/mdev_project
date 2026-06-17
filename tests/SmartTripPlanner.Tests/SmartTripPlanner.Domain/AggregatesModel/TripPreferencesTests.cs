using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class TripPreferencesTests
{
    [TestMethod]
    public void Constructor_Default_SetsDefaults()
    {
        var prefs = new TripPreferences();

        Assert.IsFalse(prefs.CarAvailable);
        Assert.AreEqual(30, prefs.MaxWalkingMinutes);
        Assert.IsTrue(prefs.WeatherAwareEnabled);
    }

    [TestMethod]
    public void Constructor_WithValues_SetsCorrectly()
    {
        var prefs = new TripPreferences(true, 60, false);

        Assert.IsTrue(prefs.CarAvailable);
        Assert.AreEqual(60, prefs.MaxWalkingMinutes);
        Assert.IsFalse(prefs.WeatherAwareEnabled);
    }

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new TripPreferences(true, 45, false);
        var b = new TripPreferences(true, 45, false);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = new TripPreferences(false, 30, true);
        var b = new TripPreferences(true, 30, true);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void MaxWalkingMinutes_Negative_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TripPreferences(false, -1));
    }
}
