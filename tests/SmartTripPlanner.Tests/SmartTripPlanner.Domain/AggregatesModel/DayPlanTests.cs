using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class DayPlanTests
{
    private static DayPlan CreateDayPlan()
    {
        var day = new DayPlan
        {
            DayIndex = 1,
            Date = new DateOnly(2026, 6, 1),
            Morning = new BlockTimeline(),
            Afternoon = new BlockTimeline(),
            Evening = new BlockTimeline()
        };
        day.SetWeather(WeatherCondition.Clear);
        return day;
    }

    [TestMethod]
    public void StartTime_DefaultIs09_00()
    {
        var dayPlan = CreateDayPlan();
        Assert.AreEqual(new TimeOnly(9, 0), dayPlan.StartTime);
    }

    [TestMethod]
    public void UpdateStartTime_ChangesValue()
    {
        var dayPlan = CreateDayPlan();
        dayPlan.UpdateStartTime(new TimeOnly(10, 30));
        Assert.AreEqual(new TimeOnly(10, 30), dayPlan.StartTime);
    }

    [TestMethod]
    public void SetWeather_ChangesWeatherSummary()
    {
        var dayPlan = CreateDayPlan();
        Assert.AreEqual(WeatherCondition.Clear, dayPlan.WeatherSummary);

        dayPlan.SetWeather(WeatherCondition.Bad);
        Assert.AreEqual(WeatherCondition.Bad, dayPlan.WeatherSummary);

        dayPlan.SetWeather(WeatherCondition.Good);
        Assert.AreEqual(WeatherCondition.Good, dayPlan.WeatherSummary);
    }

    [TestMethod]
    public void IsStale_DefaultIsFalse()
    {
        var dayPlan = CreateDayPlan();
        Assert.IsFalse(dayPlan.IsStale);
    }

    [TestMethod]
    public void MarkStale_SetsIsStaleTrue()
    {
        var dayPlan = CreateDayPlan();
        dayPlan.MarkStale();
        Assert.IsTrue(dayPlan.IsStale);
    }

    [TestMethod]
    public void ClearStale_SetsIsStaleFalse()
    {
        var dayPlan = CreateDayPlan();
        dayPlan.MarkStale();
        Assert.IsTrue(dayPlan.IsStale);
        dayPlan.ClearStale();
        Assert.IsFalse(dayPlan.IsStale);
    }

    [TestMethod]
    public void WeatherLastUpdatedAt_DefaultIsNull()
    {
        var dayPlan = CreateDayPlan();
        Assert.IsNull(dayPlan.WeatherLastUpdatedAt);
    }

    [TestMethod]
    public void UpdateWeatherTimestamp_SetsTimestamp()
    {
        var dayPlan = CreateDayPlan();
        var before = DateTimeOffset.UtcNow;
        dayPlan.UpdateWeatherTimestamp();
        var after = DateTimeOffset.UtcNow;

        Assert.IsNotNull(dayPlan.WeatherLastUpdatedAt);
        Assert.IsTrue(dayPlan.WeatherLastUpdatedAt >= before);
        Assert.IsTrue(dayPlan.WeatherLastUpdatedAt <= after);
    }
}
