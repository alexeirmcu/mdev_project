using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Tests.Infrastructure.Outbox;

[TestClass]
public sealed class OutboxMessageRepositoryTests
{
    private static PlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlannerDbContext(options);
    }

    [TestMethod]
    public async Task GetPendingAsync_WithPendingMessages_ReturnsInOrder()
    {
        using var db = CreateDbContext();
        var older = OutboxMessage.Create("ref1");
        var middle = OutboxMessage.Create("ref2");
        var newer = OutboxMessage.Create("ref3");

        db.OutboxMessages.AddRange(newer, older, middle);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        var results = await repo.GetPendingAsync(10, CancellationToken.None);

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual(older.Id, results[0].Id);
        Assert.AreEqual(middle.Id, results[1].Id);
        Assert.AreEqual(newer.Id, results[2].Id);
    }

    [TestMethod]
    public async Task GetPendingAsync_WithProcessingMessages_ExcludesThem()
    {
        using var db = CreateDbContext();
        var pending1 = OutboxMessage.Create("ref1");
        var processing = OutboxMessage.Create("ref2");
        processing.MarkProcessing();
        var pending2 = OutboxMessage.Create("ref3");

        db.OutboxMessages.AddRange(pending1, processing, pending2);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        var results = await repo.GetPendingAsync(10, CancellationToken.None);

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(m => m.Status == OutboxMessageStatus.Pending));
    }

    [TestMethod]
    public async Task GetPendingAsync_WithCompletedMessages_ExcludesThem()
    {
        using var db = CreateDbContext();
        var pending = OutboxMessage.Create("ref1");
        var completed = OutboxMessage.Create("ref2");
        completed.MarkProcessing();
        completed.MarkCompleted();

        db.OutboxMessages.AddRange(pending, completed);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        var results = await repo.GetPendingAsync(10, CancellationToken.None);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(pending.Id, results[0].Id);
    }

    [TestMethod]
    public async Task GetPendingAsync_WithFailedMessages_ExcludesThem()
    {
        using var db = CreateDbContext();
        var pending = OutboxMessage.Create("ref1");
        var failed = OutboxMessage.Create("ref2");
        failed.MarkProcessing();
        failed.MarkFailed("error");

        db.OutboxMessages.AddRange(pending, failed);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        var results = await repo.GetPendingAsync(10, CancellationToken.None);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(pending.Id, results[0].Id);
    }

    [TestMethod]
    public async Task GetPendingAsync_WithFutureNextAttemptAt_SkipsMessage()
    {
        using var db = CreateDbContext();
        var nowReady = OutboxMessage.Create("ref1");
        var future = OutboxMessage.Create("ref2");
        future.MarkProcessing();
        future.MarkCompleted();
        // Manually create a message with future NextAttemptAt by creating a pending message
        // Since ScheduleRetry sets NextAttemptAt, we need a pending-then-retry scenario
        var delayed = OutboxMessage.Create("ref3");
        delayed.MarkProcessing();
        delayed.ScheduleRetry(); // Sets NextAttemptAt to ~30s from now

        db.OutboxMessages.AddRange(nowReady, delayed);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        var results = await repo.GetPendingAsync(10, CancellationToken.None);

        // Only nowReady has no NextAttemptAt, so it's included
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(nowReady.Id, results[0].Id);
    }

    [TestMethod]
    public async Task GetPendingAsync_RespectsBatchSize()
    {
        using var db = CreateDbContext();
        for (int i = 0; i < 5; i++)
        {
            db.OutboxMessages.Add(OutboxMessage.Create($"ref{i}"));
        }
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        var results = await repo.GetPendingAsync(3, CancellationToken.None);

        Assert.AreEqual(3, results.Count);
    }

    [TestMethod]
    public async Task ReclaimExpiredLeasesAsync_ReclaimsStuckProcessing()
    {
        using var db = CreateDbContext();
        var stuck = OutboxMessage.Create("ref1");
        stuck.MarkProcessing();
        // Manually set UpdatedAt to be older than lease timeout
        typeof(OutboxMessage).GetProperty("UpdatedAt")!
            .SetValue(stuck, DateTime.UtcNow.AddMinutes(-10));

        db.OutboxMessages.Add(stuck);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        await repo.ReclaimExpiredLeasesAsync(300, CancellationToken.None);

        var reclaimed = await db.OutboxMessages.FindAsync(stuck.Id);
        Assert.IsNotNull(reclaimed);
        Assert.AreEqual(OutboxMessageStatus.Pending, reclaimed.Status);
        Assert.IsNull(reclaimed.NextAttemptAt);
    }

    [TestMethod]
    public async Task ReclaimExpiredLeasesAsync_DoesNotReclaimRecentProcessing()
    {
        using var db = CreateDbContext();
        var recent = OutboxMessage.Create("ref1");
        recent.MarkProcessing();
        // UpdatedAt is current (recent)

        db.OutboxMessages.Add(recent);
        await db.SaveChangesAsync();

        var repo = new OutboxMessageRepository(db);
        await repo.ReclaimExpiredLeasesAsync(300, CancellationToken.None);

        var stillProcessing = await db.OutboxMessages.FindAsync(recent.Id);
        Assert.IsNotNull(stillProcessing);
        Assert.AreEqual(OutboxMessageStatus.Processing, stillProcessing.Status);
    }
}
