using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Mapping;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Weather;

[TestClass]
public sealed class WeatherCodeMapperTests
{
    [TestMethod]
    public void Map_Code0_ReturnsClear()
    {
        var result = WeatherCodeMapper.Map(0, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }

    [TestMethod]
    public void Map_Code1_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(1, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code2_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(2, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code3_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(3, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code45_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(45, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_Code48_ReturnsGood()
    {
        var result = WeatherCodeMapper.Map(48, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Good, result);
    }

    [TestMethod]
    public void Map_DrizzleCode51_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(51, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_RainCode61_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(61, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_SnowCode71_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(71, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_ThunderstormCode95_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(95, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_HailCode96_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(96, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_UnmappedCode4_ReturnsClear()
    {
        var result = WeatherCodeMapper.Map(4, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }

    [TestMethod]
    public void Map_HailCode99_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(99, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_TempMaxAbove35_OverridesToBad()
    {
        var result = WeatherCodeMapper.Map(0, 35.1, 10.0);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_TempMinBelow0_OverridesToBad()
    {
        var result = WeatherCodeMapper.Map(0, 20.0, -0.1);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_TempMaxEqual35_NotBad()
    {
        var result = WeatherCodeMapper.Map(0, 35.0, 10.0);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }

    [TestMethod]
    public void Map_TempMinEqual0_NotBad()
    {
        var result = WeatherCodeMapper.Map(0, 20.0, 0.0);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }

    [TestMethod]
    public void Map_BadCodeAndExtremeTemp_ReturnsBad()
    {
        var result = WeatherCodeMapper.Map(95, 35.1, -0.1);
        Assert.AreEqual(WeatherCondition.Bad, result);
    }

    [TestMethod]
    public void Map_Code4_Unmapped_ReturnsClear()
    {
        var result = WeatherCodeMapper.Map(4, 20.0, 10.0);
        Assert.AreEqual(WeatherCondition.Clear, result);
    }
}
