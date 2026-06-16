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

    [TestMethod]
    public void Attributes_InitiallyEmpty()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.AreEqual(0, place.Attributes.Count);
    }

    [TestMethod]
    public void AddAttribute_WithValidAttribute_IncreasesCount()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        var attr = new PlaceAttribute("foursquare", "category", "Museum");

        place.AddAttribute(attr);

        Assert.AreEqual(1, place.Attributes.Count);
    }

    [TestMethod]
    public void AddAttribute_WithValidAttribute_AttributeIsPreserved()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        var attr = new PlaceAttribute("foursquare", "category", "Museum");

        place.AddAttribute(attr);

        Assert.AreSame(attr, place.Attributes[0]);
        Assert.AreEqual("foursquare", place.Attributes[0].Provider);
        Assert.AreEqual("category", place.Attributes[0].Key);
        Assert.AreEqual("Museum", place.Attributes[0].Value);
    }

    [TestMethod]
    public void AddAttribute_WithNull_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.AddAttribute(null!);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void AddAttribute_MultipleAttributes_AllPreserved()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        var attr1 = new PlaceAttribute("foursquare", "category", "Museum");
        var attr2 = new PlaceAttribute("foursquare", "chain", "Prado");

        place.AddAttribute(attr1);
        place.AddAttribute(attr2);

        Assert.AreEqual(2, place.Attributes.Count);
        Assert.AreSame(attr1, place.Attributes[0]);
        Assert.AreSame(attr2, place.Attributes[1]);
    }
}
