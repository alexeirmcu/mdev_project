using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class OpeningHoursWindowTests
{
    [TestMethod]
    public void Constructor_WithValidMinutes_SetsProperties()
    {
        var oh = new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260);
        Assert.AreEqual(DayOfWeek.Monday, oh.DayOfWeek);
        Assert.AreEqual(540, oh.OpenMinutes);
        Assert.AreEqual(1260, oh.CloseMinutes);
    }

    [TestMethod]
    public void Constructor_WithOpenAfterClose_ThrowsArgumentException()
    {
        try
        {
            _ = new OpeningHoursWindow(DayOfWeek.Monday, 1260, 540);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithOpenMinutesNegative_ThrowsArgumentOutOfRangeException()
    {
        try
        {
            _ = new OpeningHoursWindow(DayOfWeek.Monday, -1, 540);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithCloseMinutesOver1439_ThrowsArgumentOutOfRangeException()
    {
        try
        {
            _ = new OpeningHoursWindow(DayOfWeek.Monday, 0, 1440);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260);
        var b = new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260);
        Assert.AreEqual(a, b);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260);
        var b = new OpeningHoursWindow(DayOfWeek.Tuesday, 540, 1260);
        Assert.AreNotEqual(a, b);
        Assert.IsFalse(a.Equals(b));
    }
}
