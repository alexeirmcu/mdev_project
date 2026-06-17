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
}
