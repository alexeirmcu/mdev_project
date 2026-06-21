using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class ReturnToHotelStrategyTests
{
    [TestMethod]
    public void Enum_HasThreeDistinctValues()
    {
        var values = Enum.GetValues<ReturnToHotelStrategy>();
        Assert.AreEqual(3, values.Length);
        Assert.AreNotEqual(values[0], values[1]);
        Assert.AreNotEqual(values[1], values[2]);
        Assert.AreNotEqual(values[0], values[2]);
    }

    [TestMethod]
    public void DefaultValue_IsAlways()
    {
        // Default struct value for enum is 0 → should be Always
        ReturnToHotelStrategy defaultStrategy = default;
        Assert.AreEqual(ReturnToHotelStrategy.Always, defaultStrategy);
    }
}
