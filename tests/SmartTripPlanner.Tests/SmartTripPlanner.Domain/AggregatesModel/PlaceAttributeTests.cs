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
    public void Equality_SameValues_AreEqual()
    {
        var attr1 = new PlaceAttribute("foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute("foursquare", "category", "Hotel");

        Assert.AreEqual(attr1, attr2);
        Assert.IsTrue(attr1.Equals(attr2));
        Assert.AreEqual(attr1.GetHashCode(), attr2.GetHashCode());
    }

    [TestMethod]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var attr1 = new PlaceAttribute("foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute("foursquare", "category", "Restaurant");

        Assert.AreNotEqual(attr1, attr2);
    }

    [TestMethod]
    public void Equality_DifferentKey_AreNotEqual()
    {
        var attr1 = new PlaceAttribute("foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute("foursquare", "chain", "Hotel");

        Assert.AreNotEqual(attr1, attr2);
    }

    [TestMethod]
    public void Equality_DifferentProvider_AreNotEqual()
    {
        var attr1 = new PlaceAttribute("foursquare", "category", "Hotel");
        var attr2 = new PlaceAttribute("google", "category", "Hotel");

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
