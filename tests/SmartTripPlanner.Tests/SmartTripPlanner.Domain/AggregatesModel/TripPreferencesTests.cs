using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

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

    // ─────────────────────────────────────────────────────────────────────────
    // AllowMustSeeOvertime tests
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Default_AllowMustSeeOvertime_IsFalse()
    {
        var prefs = new TripPreferences();
        Assert.IsFalse(prefs.AllowMustSeeOvertime);
    }

    [TestMethod]
    public void Constructor_WithAllowMustSeeOvertime_SetsCorrectly()
    {
        var prefs = new TripPreferences(allowMustSeeOvertime: true);
        Assert.IsTrue(prefs.AllowMustSeeOvertime);

        var prefs2 = new TripPreferences(allowMustSeeOvertime: false);
        Assert.IsFalse(prefs2.AllowMustSeeOvertime);
    }

    [TestMethod]
    public void Equals_DifferentAllowMustSeeOvertime_ReturnsFalse()
    {
        var a = new TripPreferences(allowMustSeeOvertime: true);
        var b = new TripPreferences(allowMustSeeOvertime: false);

        Assert.AreNotEqual(a, b);
        Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_SameAllowMustSeeOvertime_ReturnsTrue()
    {
        var a = new TripPreferences(allowMustSeeOvertime: true);
        var b = new TripPreferences(allowMustSeeOvertime: true);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ReturnToHotelStrategy tests
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Default_ReturnToHotelStrategy_IsAlways()
    {
        var prefs = new TripPreferences();
        Assert.AreEqual(ReturnToHotelStrategy.Always, prefs.ReturnToHotelStrategy);
    }

    [TestMethod]
    public void Constructor_WithReturnToHotelStrategy_SetsCorrectly()
    {
        var prefs = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);
        Assert.AreEqual(ReturnToHotelStrategy.Never, prefs.ReturnToHotelStrategy);
    }

    [TestMethod]
    public void Equals_DifferentReturnToHotelStrategy_ReturnsFalse()
    {
        var a = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Always);
        var b = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_SameReturnToHotelStrategy_ReturnsTrue()
    {
        var a = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);
        var b = new TripPreferences(returnToHotelStrategy: ReturnToHotelStrategy.Never);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }
}
