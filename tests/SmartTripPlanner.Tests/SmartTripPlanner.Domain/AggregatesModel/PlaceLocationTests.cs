using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class PlaceLocationTests
{
    [TestMethod]
    public void Constructor_WithValidLatLng_SetsProperties()
    {
        var loc = new PlaceLocation(40.4168, -3.7038);
        Assert.AreEqual(40.4168, loc.Latitude);
        Assert.AreEqual(-3.7038, loc.Longitude);
    }

    [TestMethod]
    public void Constructor_WithLatitudeTooHigh_ThrowsArgumentOutOfRangeException()
    {
        try
        {
            _ = new PlaceLocation(91, 0);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithLatitudeTooLow_ThrowsArgumentOutOfRangeException()
    {
        try
        {
            _ = new PlaceLocation(-91, 0);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithLongitudeTooHigh_ThrowsArgumentOutOfRangeException()
    {
        try
        {
            _ = new PlaceLocation(0, 181);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithLongitudeTooLow_ThrowsArgumentOutOfRangeException()
    {
        try
        {
            _ = new PlaceLocation(0, -181);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    [TestMethod]
    public void Equals_SameCoordinates_ReturnsTrue()
    {
        var a = new PlaceLocation(40.4168, -3.7038);
        var b = new PlaceLocation(40.4168, -3.7038);
        Assert.AreEqual(a, b);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentCoordinates_ReturnsFalse()
    {
        var a = new PlaceLocation(40.4168, -3.7038);
        var b = new PlaceLocation(48.8566, 2.3522);
        Assert.AreNotEqual(a, b);
        Assert.IsFalse(a.Equals(b));
    }
}
