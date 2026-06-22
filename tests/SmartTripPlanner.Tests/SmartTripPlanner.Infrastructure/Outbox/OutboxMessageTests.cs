using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Tests.Infrastructure.Outbox;

[TestClass]
public sealed class OutboxMessageTests
{
    [TestMethod]
    public void Create_WithValidRefId_SetsDefaultState()
    {
        var message = OutboxMessage.Create("abc123");

        Assert.AreNotEqual(Guid.Empty, message.Id);
        Assert.AreEqual("abc123", message.PlaceProviderReferenceId);
        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);
        Assert.AreEqual(0, message.RetryCount);
        Assert.AreEqual(3, message.MaxRetries);
        Assert.IsNull(message.NextAttemptAt);
        Assert.IsNull(message.ProcessedAt);
        Assert.IsNull(message.PayloadJson);
        Assert.IsNull(message.Error);
        Assert.AreEqual(message.CreatedAt, message.UpdatedAt);
    }

    [TestMethod]
    public void Create_WithCustomMaxRetries_SetsMaxRetries()
    {
        var message = OutboxMessage.Create("abc123", maxRetries: 5);

        Assert.AreEqual(5, message.MaxRetries);
    }

    [TestMethod]
    public void MarkProcessing_SetsStatusAndUpdatesTimestamp()
    {
        var message = OutboxMessage.Create("abc123");
        var before = message.UpdatedAt;

        Thread.Sleep(10);
        message.MarkProcessing();

        Assert.AreEqual(OutboxMessageStatus.Processing, message.Status);
        Assert.IsTrue(message.UpdatedAt > before);
    }

    [TestMethod]
    public void MarkCompleted_SetsStatusAndProcessedAt()
    {
        var message = OutboxMessage.Create("abc123");
        message.MarkProcessing();

        message.MarkCompleted();

        Assert.AreEqual(OutboxMessageStatus.Completed, message.Status);
        Assert.IsNotNull(message.ProcessedAt);
        Assert.IsTrue(message.UpdatedAt >= message.ProcessedAt!.Value);
    }

    [TestMethod]
    public void ScheduleRetry_CalculatesBackoffAndIncrementsCount()
    {
        var message = OutboxMessage.Create("abc123");
        message.MarkProcessing();
        var before = DateTime.UtcNow;

        message.ScheduleRetry();

        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);
        Assert.AreEqual(1, message.RetryCount);
        Assert.IsNotNull(message.NextAttemptAt);
        // First retry: 2^0 * 30 = 30s
        var expectedMin = before.AddSeconds(30);
        var expectedMax = before.AddSeconds(31);
        Assert.IsTrue(message.NextAttemptAt >= expectedMin && message.NextAttemptAt <= expectedMax,
            $"Expected NextAttemptAt around {expectedMin:O}, got {message.NextAttemptAt:O}");
    }

    [TestMethod]
    public void ScheduleRetry_MultipleRetries_ExponentialBackoff()
    {
        var message = OutboxMessage.Create("abc123");

        // First retry: 2^0 * 30 = 30s
        message.MarkProcessing();
        var beforeRetry1 = DateTime.UtcNow;
        message.ScheduleRetry();
        Assert.AreEqual(1, message.RetryCount);
        var expectedBackoff1 = beforeRetry1.AddSeconds(29);
        Assert.IsTrue(message.NextAttemptAt >= expectedBackoff1,
            $"Expected NextAttemptAt >= {expectedBackoff1:O}, got {message.NextAttemptAt:O}");

        // Second retry: 2^1 * 30 = 60s
        message.MarkProcessing();
        var beforeRetry2 = DateTime.UtcNow;
        message.ScheduleRetry();
        Assert.AreEqual(2, message.RetryCount);
        var expectedBackoff2 = beforeRetry2.AddSeconds(59);
        Assert.IsTrue(message.NextAttemptAt >= expectedBackoff2,
            $"Expected NextAttemptAt >= {expectedBackoff2:O}, got {message.NextAttemptAt:O}");

        // Third retry: 2^2 * 30 = 120s
        message.MarkProcessing();
        var beforeRetry3 = DateTime.UtcNow;
        message.ScheduleRetry();
        Assert.AreEqual(3, message.RetryCount);
        var expectedBackoff3 = beforeRetry3.AddSeconds(119);
        Assert.IsTrue(message.NextAttemptAt >= expectedBackoff3,
            $"Expected NextAttemptAt >= {expectedBackoff3:O}, got {message.NextAttemptAt:O}");
    }

    [TestMethod]
    public void MarkFailed_SetsStatusErrorAndClearsNextAttempt()
    {
        var message = OutboxMessage.Create("abc123");
        message.MarkProcessing();

        message.MarkFailed("Something went wrong");

        Assert.AreEqual(OutboxMessageStatus.Failed, message.Status);
        Assert.AreEqual("Something went wrong", message.Error);
        Assert.IsNull(message.NextAttemptAt);
    }

    [TestMethod]
    public void Reclaim_ResetsToPendingWithNullNextAttempt()
    {
        var message = OutboxMessage.Create("abc123");
        message.MarkProcessing();
        message.ScheduleRetry();
        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);
        Assert.IsNotNull(message.NextAttemptAt);

        message.MarkProcessing();

        message.Reclaim();

        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);
        Assert.IsNull(message.NextAttemptAt);
    }

    [TestMethod]
    public void FullLifecycle_SuccessPath()
    {
        var message = OutboxMessage.Create("abc123");

        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);

        message.MarkProcessing();
        Assert.AreEqual(OutboxMessageStatus.Processing, message.Status);

        message.MarkCompleted();
        Assert.AreEqual(OutboxMessageStatus.Completed, message.Status);
        Assert.IsNotNull(message.ProcessedAt);
    }

    [TestMethod]
    public void FullLifecycle_RetryThenFail()
    {
        var message = OutboxMessage.Create("abc123", maxRetries: 3);

        message.MarkProcessing();
        message.ScheduleRetry();
        Assert.AreEqual(1, message.RetryCount);
        Assert.AreEqual(OutboxMessageStatus.Pending, message.Status);

        message.MarkProcessing();
        message.ScheduleRetry();
        Assert.AreEqual(2, message.RetryCount);

        message.MarkProcessing();
        message.ScheduleRetry();
        Assert.AreEqual(3, message.RetryCount);

        message.MarkProcessing();
        message.MarkFailed("Final failure");
        Assert.AreEqual(OutboxMessageStatus.Failed, message.Status);
        Assert.AreEqual("Final failure", message.Error);
    }
}
