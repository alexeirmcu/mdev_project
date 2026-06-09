using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class ActivityNodeTests
{
    private static ActivityNode CreateNode() => new()
    {
        PlaceId = "place1",
        Name = "Test Activity",
        SequenceOrder = 1
    };

    [TestMethod]
    public void Priority_DefaultIsMedium()
    {
        var node = CreateNode();
        Assert.AreEqual(Priority.MEDIUM, node.Priority);
    }

    [TestMethod]
    public void MarkAsCompleted_SetsIsCompletedTrue()
    {
        var node = CreateNode();
        Assert.IsFalse(node.IsCompleted);
        node.MarkAsCompleted();
        Assert.IsTrue(node.IsCompleted);
    }
}
