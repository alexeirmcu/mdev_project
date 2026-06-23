using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class CityTests
{
    [TestMethod]
    public void Constructor_WithoutCoordinates_LatitudeAndLongitudeAreNull()
    {
        var city = new City("madrid", "Madrid");

        Assert.IsNull(city.Latitude);
        Assert.IsNull(city.Longitude);
    }

    [TestMethod]
    public void Constructor_WithCoordinates_SetsLatitudeAndLongitude()
    {
        var city = new City("madrid", "Madrid", true, 40.4168, -3.7038);

        Assert.AreEqual(40.4168, city.Latitude);
        Assert.AreEqual(-3.7038, city.Longitude);
    }

    [TestMethod]
    public void Constructor_DefaultCoordinatesAreNull()
    {
        var city = new City("madrid", "Madrid");

        Assert.IsNull(city.Latitude);
        Assert.IsNull(city.Longitude);
    }

    [TestMethod]
    public void Constructor_WithOnlyCodeAndName_SetsOtherPropertiesCorrectly()
    {
        var city = new City("paris", "Paris");

        Assert.AreEqual("paris", city.CityCode);
        Assert.AreEqual("Paris", city.CityName);
        Assert.IsTrue(city.IsAllowed);
    }

    [TestMethod]
    public void Constructor_ExplicitIsAllowedFalse_SetsIsAllowedFalse()
    {
        var city = new City("blocked", "Blocked City", false);

        Assert.IsFalse(city.IsAllowed);
    }
}
