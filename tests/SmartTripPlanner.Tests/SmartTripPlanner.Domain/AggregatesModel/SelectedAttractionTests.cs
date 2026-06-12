using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class SelectedAttractionTests
{
    [TestMethod]
    public void Constructor_WithValidPlaceIdAndName_SetsProperties()
    {
        var attraction = new SelectedAttraction("place-1", "Eiffel Tower");

        Assert.AreEqual("place-1", attraction.PlaceId);
        Assert.AreEqual("Eiffel Tower", attraction.Name);
    }

    [TestMethod]
    public void Constructor_WithNullPlaceId_ThrowsSmartTripDomainException()
    {
        try
        {
            _ = new SelectedAttraction(null!, "Name");
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
            _ = new SelectedAttraction("place-1", null!);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException)
        {
        }
    }

    [TestMethod]
    public void Equals_SamePlaceId_ReturnsTrue()
    {
        var a1 = new SelectedAttraction("place-1", "Eiffel Tower");
        var a2 = new SelectedAttraction("place-1", "Tour Eiffel");

        Assert.AreEqual(a1, a2);
    }

    [TestMethod]
    public void Equals_DifferentPlaceId_ReturnsFalse()
    {
        var a1 = new SelectedAttraction("place-1", "Eiffel Tower");
        var a2 = new SelectedAttraction("place-2", "Eiffel Tower");

        Assert.AreNotEqual(a1, a2);
    }

    [TestMethod]
    public void Equals_WithNull_ReturnsFalse()
    {
        var attraction = new SelectedAttraction("place-1", "Eiffel Tower");

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.IsFalse(attraction.Equals(null));
#pragma warning restore CS8625
    }

    [TestMethod]
    public void GetHashCode_SamePlaceId_ReturnsSameHashCode()
    {
        var a1 = new SelectedAttraction("place-1", "Eiffel Tower");
        var a2 = new SelectedAttraction("place-1", "Tour Eiffel");

        Assert.AreEqual(a1.GetHashCode(), a2.GetHashCode());
    }
}
