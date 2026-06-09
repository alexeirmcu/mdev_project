using SmartTripPlanner.Infrastructure.Configuration;

namespace SmartTripPlanner.Tests.Infrastructure.Configuration;

[TestClass]
public sealed class FoursquareApiOptionsTests
{
    [TestMethod]
    public void FoursquareApiOptions_DefaultValues_AreCorrect()
    {
        var options = new FoursquareApiOptions();

        Assert.AreEqual("https://api.foursquare.com/v3/", options.BaseUrl);
        Assert.AreEqual(string.Empty, options.ApiKey);
    }

    [TestMethod]
    public void FoursquareApiOptions_SectionName_IsFoursquareApi()
    {
        Assert.AreEqual("FoursquareApi", FoursquareApiOptions.SectionName);
    }
}
