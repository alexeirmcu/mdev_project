using SmartTripPlanner.Infrastructure.LLM;

namespace SmartTripPlanner.Tests.Infrastructure.LLM;

[TestClass]
public sealed class PlaceEnrichmentResponseTests
{
    [TestMethod]
    public void Validate_WithValidValues_DoesNotThrow()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 120,
            IsIndoor = true,
            FamilyFriendlyScore = 4,
            Popularity = 0.8
        };

        response.Validate();
    }

    [TestMethod]
    public void Validate_WithMinValidValues_DoesNotThrow()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 15,
            IsIndoor = false,
            FamilyFriendlyScore = 1,
            Popularity = 0.0
        };

        response.Validate();
    }

    [TestMethod]
    public void Validate_WithMaxValidValues_DoesNotThrow()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 480,
            IsIndoor = true,
            FamilyFriendlyScore = 5,
            Popularity = 1.0
        };

        response.Validate();
    }

    [TestMethod]
    public void Validate_WithDurationBelow15_Throws()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 10,
            IsIndoor = true,
            FamilyFriendlyScore = 3,
            Popularity = 0.5
        };

        try
        {
            response.Validate();
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("TypicalDurationMinutes"));
        }
    }

    [TestMethod]
    public void Validate_WithDurationAbove480_Throws()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 500,
            IsIndoor = true,
            FamilyFriendlyScore = 3,
            Popularity = 0.5
        };

        try
        {
            response.Validate();
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("TypicalDurationMinutes"));
        }
    }

    [TestMethod]
    public void Validate_WithScoreBelow1_Throws()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 60,
            IsIndoor = true,
            FamilyFriendlyScore = 0,
            Popularity = 0.5
        };

        try
        {
            response.Validate();
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("FamilyFriendlyScore"));
        }
    }

    [TestMethod]
    public void Validate_WithScoreAbove5_Throws()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 60,
            IsIndoor = true,
            FamilyFriendlyScore = 7,
            Popularity = 0.5
        };

        try
        {
            response.Validate();
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("FamilyFriendlyScore"));
        }
    }

    [TestMethod]
    public void Validate_WithPopularityBelow0_Throws()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 60,
            IsIndoor = true,
            FamilyFriendlyScore = 3,
            Popularity = -0.1
        };

        try
        {
            response.Validate();
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("Popularity"));
        }
    }

    [TestMethod]
    public void Validate_WithPopularityAbove1_Throws()
    {
        var response = new PlaceEnrichmentResponse
        {
            TypicalDurationMinutes = 60,
            IsIndoor = true,
            FamilyFriendlyScore = 3,
            Popularity = 1.5
        };

        try
        {
            response.Validate();
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("Popularity"));
        }
    }
}
