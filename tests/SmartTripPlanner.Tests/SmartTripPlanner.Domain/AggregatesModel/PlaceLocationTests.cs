using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Exceptions;

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
    public void Constructor_WithLatitudeTooHigh_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceLocation(91, 0);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithLatitudeTooLow_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceLocation(-91, 0);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithLongitudeTooHigh_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceLocation(0, 181);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithLongitudeTooLow_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceLocation(0, -181);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
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

    [TestMethod]
    public void DistanceKmTo_SamePoint_ReturnsZero()
    {
        var loc = new PlaceLocation(40.4168, -3.7038);
        var distance = loc.DistanceKmTo(loc);
        Assert.AreEqual(0, distance, 0.001);
    }

    [TestMethod]
    public void DistanceKmTo_PuertaDelSolToPlazaMayor_Approximately0_6km()
    {
        // Puerta del Sol, Madrid
        var sol = new PlaceLocation(40.4168, -3.7038);
        // Plaza Mayor, Madrid (~0.3 km away)
        var plazaMayor = new PlaceLocation(40.4154, -3.7074);

        var distance = sol.DistanceKmTo(plazaMayor);
        Assert.AreEqual(0.3, distance, 0.15);
    }

    [TestMethod]
    public void DistanceKmTo_MadridToParis_Approximately1050km()
    {
        var madrid = new PlaceLocation(40.4168, -3.7038);
        var paris = new PlaceLocation(48.8566, 2.3522);

        var distance = madrid.DistanceKmTo(paris);
        Assert.AreEqual(1050, distance, 50);
    }

    [TestMethod]
    public void DistanceKmTo_IsSymmetric()
    {
        var a = new PlaceLocation(40.4168, -3.7038);
        var b = new PlaceLocation(48.8566, 2.3522);

        var distAB = a.DistanceKmTo(b);
        var distBA = b.DistanceKmTo(a);
        Assert.AreEqual(distAB, distBA, 0.001);
    }
}
