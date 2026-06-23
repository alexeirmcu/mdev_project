using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Tests.Domain.Exceptions;

[TestClass]
public sealed class TripForbiddenExceptionTests
{
    [TestMethod]
    public void Constructor_WithTripIdAndCaller_SetsMessage()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var caller = "user-99";

        // Act
        var exception = new TripForbiddenException(tripId, caller);

        // Assert
        Assert.IsNotNull(exception);
        Assert.IsTrue(exception.Message.Contains(tripId.ToString()));
        Assert.IsTrue(exception.Message.Contains(caller));
    }

    [TestMethod]
    public void Constructor_WithTripIdAndCaller_IsSmartTripDomainException()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var caller = "user-99";

        // Act
        var exception = new TripForbiddenException(tripId, caller);

        // Assert
        Assert.IsInstanceOfType(exception, typeof(SmartTripDomainException));
    }
}
