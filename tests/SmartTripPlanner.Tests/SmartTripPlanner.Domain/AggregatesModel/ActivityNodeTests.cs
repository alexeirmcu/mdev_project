using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class ActivityNodeTests
{
    private static ActivityNode CreateNode() => new()
    {
        PlaceId = 1L,
        Name = "Test Activity",
        SequenceOrder = 1,
        DurationMinutes = 60,
        IsIndoor = false
    };

    [TestMethod]
    public void Constructor_WithLongPlaceId_CreatesActivity()
    {
        var node = new ActivityNode(
            placeId: 42,
            name: "Eiffel Tower",
            sequenceOrder: 1,
            durationMinutes: 90,
            isIndoor: false);

        Assert.AreEqual(42L, node.PlaceId);
        Assert.AreEqual("Eiffel Tower", node.Name);
        Assert.AreEqual(1, node.SequenceOrder);
        Assert.AreEqual(90, node.DurationMinutes);
    }

    [TestMethod]
    public void Priority_DefaultIsMedium()
    {
        var node = CreateNode();
        Assert.AreEqual(Priority.Medium, node.Priority);
    }

    [TestMethod]
    public void MarkAsCompleted_SetsIsCompletedTrue()
    {
        var node = CreateNode();
        Assert.IsFalse(node.IsCompleted);
        node.MarkAsCompleted();
        Assert.IsTrue(node.IsCompleted);
    }

    [TestMethod]
    public void SetCompleted_True_SetsIsCompletedTrue()
    {
        var node = CreateNode();
        node.SetCompleted(true);
        Assert.IsTrue(node.IsCompleted);
    }

    [TestMethod]
    public void SetCompleted_False_SetsIsCompletedFalse()
    {
        var node = CreateNode();
        node.SetCompleted(true);
        Assert.IsTrue(node.IsCompleted);
        node.SetCompleted(false);
        Assert.IsFalse(node.IsCompleted);
    }

    [TestMethod]
    public void SetCompleted_False_AfterMarkAsCompleted_SetsIsCompletedFalse()
    {
        var node = CreateNode();
        node.MarkAsCompleted();
        Assert.IsTrue(node.IsCompleted);
        node.SetCompleted(false);
        Assert.IsFalse(node.IsCompleted);
    }

    [TestMethod]
    public void MarkAsCompleted_StillWorks_AfterSetCompleted()
    {
        var node = CreateNode();
        node.SetCompleted(false);
        Assert.IsFalse(node.IsCompleted);
        node.MarkAsCompleted();
        Assert.IsTrue(node.IsCompleted);
    }
}
