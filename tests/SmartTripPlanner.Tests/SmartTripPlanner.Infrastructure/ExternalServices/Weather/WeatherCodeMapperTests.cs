using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Mapping;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Weather;

[TestClass]
public sealed class WeatherCodeMapperTests
{
    [TestMethod]
    public void Map_Code0_ReturnsClear()
    {
        var result = WeatherCodeMapper.Map(0);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }

    [TestMethod]
    public void Map_Code1_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(1);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code2_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(2);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code3_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(3);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code45_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(45);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code48_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(48);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_DrizzleCode51_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(51);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_RainCode61_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(61);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_SnowCode71_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(71);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_ThunderstormCode95_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(95);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_HailCode96_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(96);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_UnmappedCode4_ReturnsClear()
    {
        var result = WeatherCodeMapper.Map(4);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }

    [TestMethod]
    public void Map_HailCode99_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(99);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_Code4_Unmapped_ReturnsClear()
    {
        var result = WeatherCodeMapper.Map(4);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }
}
