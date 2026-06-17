using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Exceptions;

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
    public void Constructor_WithOpenAfterClose_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new OpeningHoursWindow(DayOfWeek.Monday, 1260, 540);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithOpenMinutesNegative_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new OpeningHoursWindow(DayOfWeek.Monday, -1, 540);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithCloseMinutesOver1439_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new OpeningHoursWindow(DayOfWeek.Monday, 0, 1440);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
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

    [TestMethod]
    public void IsOpenOn_SameDay_ReturnsTrue()
    {
        var oh = new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260);
        Assert.IsTrue(oh.IsOpenOn(DayOfWeek.Monday));
    }

    [TestMethod]
    public void IsOpenOn_DifferentDay_ReturnsFalse()
    {
        var oh = new OpeningHoursWindow(DayOfWeek.Monday, 540, 1260);
        Assert.IsFalse(oh.IsOpenOn(DayOfWeek.Tuesday));
    }

    [TestMethod]
    public void IsOpenOn_WeekendCheck_ReturnsCorrectly()
    {
        var oh = new OpeningHoursWindow(DayOfWeek.Saturday, 600, 1200);
        Assert.IsTrue(oh.IsOpenOn(DayOfWeek.Saturday));
        Assert.IsFalse(oh.IsOpenOn(DayOfWeek.Friday));
    }
}
