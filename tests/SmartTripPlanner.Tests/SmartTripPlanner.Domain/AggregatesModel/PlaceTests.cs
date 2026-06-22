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

        Assert.AreSame(attr, place.Attributes.First());
        Assert.AreEqual("foursquare", place.Attributes.First().Provider);
        Assert.AreEqual("category", place.Attributes.First().Key);
        Assert.AreEqual("Museum", place.Attributes.First().Value);
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
        Assert.AreSame(attr1, place.Attributes.First());
        Assert.AreSame(attr2, place.Attributes.ElementAt(1));
    }

    [TestMethod]
    public void DefaultFamilyFriendlyScore_Is3()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.AreEqual(3, place.FamilyFriendlyScore);
    }

    [TestMethod]
    public void DefaultPopularity_Is05()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.AreEqual(0.5, place.Popularity);
    }

    [TestMethod]
    public void DefaultIsEnriched_IsFalse()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);
        Assert.IsFalse(place.IsEnriched);
    }

    [TestMethod]
    public void MarkEnriched_WithValidInputs_SetsAllFieldsAndIsEnriched()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        place.MarkEnriched(120, true, 4, 0.8);

        Assert.IsTrue(place.IsEnriched);
        Assert.AreEqual(120, place.TypicalDurationMinutes);
        Assert.IsTrue(place.IsIndoor);
        Assert.AreEqual(4, place.FamilyFriendlyScore);
        Assert.AreEqual(0.8, place.Popularity);
    }

    [TestMethod]
    public void MarkEnriched_WithMinValidValues_SetsFields()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        place.MarkEnriched(1, false, 1, 0.0);

        Assert.IsTrue(place.IsEnriched);
        Assert.AreEqual(1, place.TypicalDurationMinutes);
        Assert.IsFalse(place.IsIndoor);
        Assert.AreEqual(1, place.FamilyFriendlyScore);
        Assert.AreEqual(0.0, place.Popularity);
    }

    [TestMethod]
    public void MarkEnriched_WithMaxValidValues_SetsFields()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        place.MarkEnriched(480, true, 5, 1.0);

        Assert.IsTrue(place.IsEnriched);
        Assert.AreEqual(480, place.TypicalDurationMinutes);
        Assert.IsTrue(place.IsIndoor);
        Assert.AreEqual(5, place.FamilyFriendlyScore);
        Assert.AreEqual(1.0, place.Popularity);
    }

    [TestMethod]
    public void MarkEnriched_WithFamilyFriendlyScoreBelow1_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(60, true, 0, 0.5);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException ex)
        {
            Assert.IsTrue(ex.Message.Contains("FamilyFriendlyScore"));
        }
    }

    [TestMethod]
    public void MarkEnriched_WithFamilyFriendlyScoreAbove5_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(60, true, 6, 0.5);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException ex)
        {
            Assert.IsTrue(ex.Message.Contains("FamilyFriendlyScore"));
        }
    }

    [TestMethod]
    public void MarkEnriched_WithPopularityBelow0_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(60, true, 3, -0.1);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException ex)
        {
            Assert.IsTrue(ex.Message.Contains("Popularity"));
        }
    }

    [TestMethod]
    public void MarkEnriched_WithPopularityAbove1_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(60, true, 3, 1.5);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException ex)
        {
            Assert.IsTrue(ex.Message.Contains("Popularity"));
        }
    }

    [TestMethod]
    public void MarkEnriched_WithDurationZero_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(0, true, 3, 0.5);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException ex)
        {
            Assert.IsTrue(ex.Message.Contains("TypicalDurationMinutes"));
        }
    }

    [TestMethod]
    public void MarkEnriched_WithDurationNegative_ThrowsSmartTripDomainException()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(-1, true, 3, 0.5);
            Assert.Fail("Expected SmartTripDomainException was not thrown");
        }
        catch (SmartTripDomainException ex)
        {
            Assert.IsTrue(ex.Message.Contains("TypicalDurationMinutes"));
        }
    }

    [TestMethod]
    public void MarkEnriched_AfterException_DoesNotMutateFields()
    {
        var place = new Place("fsq123", "Museo del Prado", 1L, ValidLocation);

        try
        {
            place.MarkEnriched(60, true, 6, 0.5);
        }
        catch (SmartTripDomainException)
        {
        }

        Assert.IsFalse(place.IsEnriched);
        Assert.AreEqual(60, place.TypicalDurationMinutes);
        Assert.IsFalse(place.IsIndoor);
        Assert.AreEqual(3, place.FamilyFriendlyScore);
        Assert.AreEqual(0.5, place.Popularity);
    }
}
