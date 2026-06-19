using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class PlaceAttributeTests
{
    [TestMethod]
    public void Constructor_WithValidData_SetsProperties()
    {
        var attr = new PlaceAttribute("foursquare", "category", "Hotel");

        Assert.AreEqual("foursquare", attr.Provider);
        Assert.AreEqual("category", attr.Key);
        Assert.AreEqual("Hotel", attr.Value);
    }

    [TestMethod]
    public void Id_Default_IsZero_Transient()
    {
        var attr = new PlaceAttribute("foursquare", "category", "Hotel");

        Assert.AreEqual(0L, attr.Id);
        Assert.IsTrue(attr.IsTransient());
    }

    [TestMethod]
    public void EntityEquals_SameIdentity_AreEqual()
    {
        var attr1 = new PlaceAttribute("foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute("foursquare", "category", "Hotel");

        // Entity equality is identity-based — transient (Id=0) objects are NOT equal
        Assert.IsFalse(attr1.Equals(attr2));
        Assert.AreNotEqual(attr1.GetHashCode(), attr2.GetHashCode());
    }

    [TestMethod]
    public void EntityEquals_SameId_AreEqual()
    {
        var attr1 = new PlaceAttribute(1, "foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute(1, "foursquare", "category", "Hotel");

        Assert.AreEqual(attr1, attr2);
        Assert.AreEqual(attr1.GetHashCode(), attr2.GetHashCode());
    }

    [TestMethod]
    public void EntityEquals_DifferentId_AreNotEqual()
    {
        var attr1 = new PlaceAttribute(1, "foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute(2, "foursquare", "category", "Museum");

        Assert.AreNotEqual(attr1, attr2);
    }

    [TestMethod]
    public void Constructor_WithNullProvider_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceAttribute(null!, "category", "Hotel");
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithEmptyProvider_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceAttribute("", "category", "Hotel");
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithNullKey_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceAttribute("foursquare", null!, "Hotel");
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithEmptyKey_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceAttribute("foursquare", "", "Hotel");
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithNullValue_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceAttribute("foursquare", "category", null!);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Constructor_WithEmptyValue_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new PlaceAttribute("foursquare", "category", "");
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }
}
