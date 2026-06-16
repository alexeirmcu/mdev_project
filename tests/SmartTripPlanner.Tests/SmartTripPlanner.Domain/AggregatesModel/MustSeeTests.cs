using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class MustSeeTests
{
    [TestMethod]
    public void Constructor_WithRequiredParameters_SetsProperties()
    {
        var mustSee = new MustSee(42L, Priority.HIGH);

        Assert.AreEqual(42L, mustSee.PlaceId);
        Assert.AreEqual(Priority.HIGH, mustSee.Priority);
        Assert.IsNull(mustSee.PinnedDayIndex);
        Assert.IsNull(mustSee.PinnedBlock);
    }

    [TestMethod]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        var mustSee = new MustSee(42L, Priority.MEDIUM, 1, BlockType.MORNING);

        Assert.AreEqual(42L, mustSee.PlaceId);
        Assert.AreEqual(Priority.MEDIUM, mustSee.Priority);
        Assert.AreEqual(1, mustSee.PinnedDayIndex);
        Assert.AreEqual(BlockType.MORNING, mustSee.PinnedBlock);
    }

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new MustSee(42L, Priority.HIGH, 0, BlockType.MORNING);
        var b = new MustSee(42L, Priority.HIGH, 0, BlockType.MORNING);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentPlaceId_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.HIGH);
        var b = new MustSee(43L, Priority.HIGH);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_DifferentPriority_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.HIGH);
        var b = new MustSee(42L, Priority.LOW);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_DifferentPinnedDayIndex_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.HIGH, 0);
        var b = new MustSee(42L, Priority.HIGH, 1);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_NullPinnedDayIndexVsZero_ReturnsFalse()
    {
        var a = new MustSee(42L, Priority.HIGH, null);
        var b = new MustSee(42L, Priority.HIGH, 0);

        Assert.AreNotEqual(a, b);
    }
}
