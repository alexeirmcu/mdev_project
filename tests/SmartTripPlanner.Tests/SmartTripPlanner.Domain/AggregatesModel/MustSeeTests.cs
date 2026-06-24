using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class MustSeeTests
{
    [TestMethod]
    public void Constructor_WithRequiredParameters_SetsProperties()
    {
        var mustSee = new MustSee(42L, Priority.High);

        Assert.AreEqual(42L, mustSee.PlaceId);
        Assert.AreEqual(Priority.High, mustSee.Priority);
        Assert.IsNull(mustSee.PinnedDayIndex);
        Assert.IsNull(mustSee.PinnedBlock);
    }

    [TestMethod]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        var mustSee = new MustSee(42L, Priority.Medium, 1, BlockType.Morning);

        Assert.AreEqual(42L, mustSee.PlaceId);
        Assert.AreEqual(Priority.Medium, mustSee.Priority);
        Assert.AreEqual(1, mustSee.PinnedDayIndex);
        Assert.AreEqual(BlockType.Morning, mustSee.PinnedBlock);
    }

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new MustSee(42L, Priority.High, 0, BlockType.Morning);
        var b = new MustSee(42L, Priority.High, 0, BlockType.Morning);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentPlaceId_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.High);
        var b = new MustSee(43L, Priority.High);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_DifferentPriority_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.High);
        var b = new MustSee(42L, Priority.Low);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_DifferentPinnedDayIndex_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.High, 0);
        var b = new MustSee(42L, Priority.High, 1);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_NullPinnedDayIndexVsZero_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.High, null);
        var b = new MustSee(42L, Priority.High, 0);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void ForceIncludeDespiteWeather_DefaultIsFalse()
    {
        var mustSee = new MustSee(42L, Priority.High);
        Assert.IsFalse(mustSee.ForceIncludeDespiteWeather);
    }

    [TestMethod]
    public void ForceIncludeDespiteWeather_CanSetToTrue()
    {
        var mustSee = new MustSee(42L, Priority.High, forceIncludeDespiteWeather: true);
        Assert.IsTrue(mustSee.ForceIncludeDespiteWeather);
    }

    [TestMethod]
    public void Equals_SameValuesWithForceIncludeTrue_ReturnsTrue()
    {
        var a = new MustSee(42L, Priority.High, 0, BlockType.Morning, forceIncludeDespiteWeather: true);
        var b = new MustSee(42L, Priority.High, 0, BlockType.Morning, forceIncludeDespiteWeather: true);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentForceInclude_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.High, forceIncludeDespiteWeather: true);
        var b = new MustSee(42L, Priority.High, forceIncludeDespiteWeather: false);

        Assert.AreNotEqual(a, b);
    }
}
