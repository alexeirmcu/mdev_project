using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class PlaceTests
{
    private static readonly PlaceLocation ValidLocation = new(40.4168, -3.7038);

    [TestMethod]
    public void Constructor_WithValidFields_SetsProperties()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.AreEqual("fsq123", place.ProviderReferenceId);
        Assert.AreEqual("Museo del Prado", place.Name);
        Assert.AreEqual(1L, place.CityId);
        Assert.AreEqual(ValidLocation, place.Location);
    }

    [TestMethod]
    public void Constructor_WithNullProviderReferenceId_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new Place(null!, "Museo del Prado", 1L, ValidLocation);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithEmptyProviderReferenceId_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new Place("", "Museo del Prado", 1L, ValidLocation);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithNullName_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new Place("fsq123", null!, 1L, ValidLocation);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithNullLocation_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new Place("fsq123", "Museo del Prado", 1L, null!);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void DefaultTypicalDurationMinutes_Is60()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.AreEqual(60, place.TypicalDurationMinutes);
    }

    [TestMethod]
    public void DefaultIsIndoor_IsFalse()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.IsFalse(place.IsIndoor);
    }

    [TestMethod]
    public void DefaultIsFamilyFriendly_IsTrue()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.IsTrue(place.IsFamilyFriendly);
    }

    [TestMethod]
    public void OpeningHours_InitiallyEmpty()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.AreEqual(0, place.OpeningHours.Count);
    }
}
