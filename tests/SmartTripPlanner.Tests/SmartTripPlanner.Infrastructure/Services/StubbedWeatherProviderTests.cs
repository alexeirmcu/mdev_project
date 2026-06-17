using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Infrastructure.Services;

namespace SmartTripPlanner.Tests.Infrastructure.Services;

[TestClass]
public sealed class StubbedWeatherProviderTests
{
    private readonly StubbedWeatherProvider _provider = new();

    [TestMethod]
    public async Task GetWeatherAsync_SingleDate_ReturnsClear()
    {
        var result = await _provider.GetWeatherAsync(1, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(WeatherCondition.Clear, result[new DateOnly(2026, 6, 1)]);
    }

    [TestMethod]
    public async Task GetWeatherAsync_MultiDayRange_AllDatesClear()
    {
        var result = await _provider.GetWeatherAsync(
            1,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 5));

        Assert.AreEqual(5, result.Count);
        foreach (var weather in result.Values)
        {
            Assert.AreEqual(WeatherCondition.Clear, weather);
        }
    }

    [TestMethod]
    public async Task GetWeatherAsync_AllDatesHaveCorrectKeys()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 3);

        var result = await _provider.GetWeatherAsync(1, start, end);

        Assert.IsTrue(result.ContainsKey(new DateOnly(2026, 6, 1)));
        Assert.IsTrue(result.ContainsKey(new DateOnly(2026, 6, 2)));
        Assert.IsTrue(result.ContainsKey(new DateOnly(2026, 6, 3)));
    }

    [TestMethod]
    public async Task GetWeatherAsync_CityId_DoesNotAffectResult()
    {
        var result1 = await _provider.GetWeatherAsync(1, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));
        var result2 = await _provider.GetWeatherAsync(999, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

        Assert.AreEqual(WeatherCondition.Clear, result1[new DateOnly(2026, 6, 1)]);
        Assert.AreEqual(WeatherCondition.Clear, result2[new DateOnly(2026, 6, 1)]);
    }
}
