using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class OverConstrainedRouteExceptionTests
{
    [TestMethod]
    public void Constructor_WithLongPlaceIds_SetsConflictingPlaceIds()
    {
        var placeIds = new List<long> { 1001, 1002, 1003 };
        var ex = new OverConstrainedRouteException(placeIds);

        Assert.AreEqual(3, ex.ConflictingPlaceIds.Count);
        Assert.IsTrue(ex.ConflictingPlaceIds.Contains(1001));
        Assert.IsTrue(ex.ConflictingPlaceIds.Contains(1002));
        Assert.IsTrue(ex.ConflictingPlaceIds.Contains(1003));
    }

    [TestMethod]
    public void Constructor_WithSinglePlaceId_WorksCorrectly()
    {
        var placeIds = new List<long> { 42 };
        var ex = new OverConstrainedRouteException(placeIds);

        Assert.AreEqual(1, ex.ConflictingPlaceIds.Count);
        Assert.AreEqual(42, ex.ConflictingPlaceIds[0]);
    }

    [TestMethod]
    public void Constructor_WithEmptyList_WorksCorrectly()
    {
        var ex = new OverConstrainedRouteException(new List<long>());

        Assert.AreEqual(0, ex.ConflictingPlaceIds.Count);
    }

    [TestMethod]
    public void Constructor_Message_IsDescriptive()
    {
        var ex = new OverConstrainedRouteException(new List<long> { 1L });
        Assert.IsTrue(ex.Message.Contains("over-constrained", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Constructor_InheritsFromSmartTripDomainException()
    {
        var ex = new OverConstrainedRouteException(new List<long> { 1L });
        Assert.IsInstanceOfType(ex, typeof(SmartTripDomainException));
    }
}
