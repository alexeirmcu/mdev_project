using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class DayPlanTests
{
    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static DayPlan CreateDayPlan()
    {
        var day = new DayPlan(
            1,
            FutureStartDate,
            new BlockTimeline { BlockType = BlockType.Morning },
            new BlockTimeline { BlockType = BlockType.Afternoon },
            new BlockTimeline { BlockType = BlockType.Evening }
        );
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

    [TestMethod]
    public void Constructor_ValidBlocks_CreatesDayPlan()
    {
        var morning = new BlockTimeline { BlockType = BlockType.Morning };
        var afternoon = new BlockTimeline { BlockType = BlockType.Afternoon };
        var evening = new BlockTimeline { BlockType = BlockType.Evening };

        var day = new DayPlan(0, FutureStartDate, morning, afternoon, evening);

        Assert.AreEqual(3, day.Blocks.Count);
        Assert.AreSame(morning, day.GetBlock(BlockType.Morning));
        Assert.AreSame(afternoon, day.GetBlock(BlockType.Afternoon));
        Assert.AreSame(evening, day.GetBlock(BlockType.Evening));
    }

    [TestMethod]
    public void Constructor_WrongBlockType_ThrowsArgumentException()
    {
        var morning = new BlockTimeline { BlockType = BlockType.Morning };
        var afternoon = new BlockTimeline { BlockType = BlockType.Afternoon };
        var wrongTimeline = new BlockTimeline { BlockType = BlockType.Morning }; // Should be Evening

        Assert.ThrowsExactly<ArgumentException>(() =>
            new DayPlan(0, FutureStartDate, morning, afternoon, wrongTimeline));
    }

    [TestMethod]
    public void GetBlock_ReturnsCorrectBlock()
    {
        var day = CreateDayPlan();
        Assert.AreEqual(BlockType.Morning, day.GetBlock(BlockType.Morning).BlockType);
        Assert.AreEqual(BlockType.Afternoon, day.GetBlock(BlockType.Afternoon).BlockType);
        Assert.AreEqual(BlockType.Evening, day.GetBlock(BlockType.Evening).BlockType);
    }

    [TestMethod]
    public void Blocks_ReturnsAllThreeBlocks()
    {
        var day = CreateDayPlan();
        Assert.AreEqual(3, day.Blocks.Count);
        Assert.AreEqual(BlockType.Morning, day.Blocks[0].BlockType);
        Assert.AreEqual(BlockType.Afternoon, day.Blocks[1].BlockType);
        Assert.AreEqual(BlockType.Evening, day.Blocks[2].BlockType);
    }
}
