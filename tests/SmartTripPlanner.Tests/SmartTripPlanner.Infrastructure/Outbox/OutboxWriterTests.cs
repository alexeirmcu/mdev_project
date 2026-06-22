using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Tests.Infrastructure.Outbox;

[TestClass]
public sealed class OutboxWriterTests
{
    private static PlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlannerDbContext(options);
    }

    [TestMethod]
    public async Task EnqueueAsync_WithNewRefIds_AddsMessages()
    {
        using var db = CreateDbContext();
        var writer = new OutboxWriter(db);

        await writer.EnqueueAsync(new[] { "ref1", "ref2" });

        Assert.AreEqual(2, db.OutboxMessages.Local.Count);
    }

    [TestMethod]
    public async Task EnqueueAsync_WithEmptyList_DoesNotAddMessages()
    {
        using var db = CreateDbContext();
        var writer = new OutboxWriter(db);

        await writer.EnqueueAsync(Array.Empty<string>());

        Assert.AreEqual(0, db.OutboxMessages.Local.Count);
    }

    [TestMethod]
    public async Task EnqueueAsync_WithDuplicateRefId_SkipsExistingPending()
    {
        using var db = CreateDbContext();
        var existing = OutboxMessage.Create("ref1");
        db.OutboxMessages.Add(existing);
        await db.SaveChangesAsync();

        var writer = new OutboxWriter(db);
        await writer.EnqueueAsync(new[] { "ref1", "ref2" });

        Assert.AreEqual(2, db.OutboxMessages.Local.Count);
    }

    [TestMethod]
    public async Task EnqueueAsync_WithDuplicateRefId_SkipsExistingProcessing()
    {
        using var db = CreateDbContext();
        var existing = OutboxMessage.Create("ref1");
        existing.MarkProcessing();
        db.OutboxMessages.Add(existing);
        await db.SaveChangesAsync();

        var writer = new OutboxWriter(db);
        await writer.EnqueueAsync(new[] { "ref1" });

        Assert.AreEqual(1, db.OutboxMessages.Local.Count);
    }

    [TestMethod]
    public async Task EnqueueAsync_ExistingCompleted_AllowsDuplicate()
    {
        using var db = CreateDbContext();
        var existing = OutboxMessage.Create("ref1");
        existing.MarkProcessing();
        existing.MarkCompleted();
        db.OutboxMessages.Add(existing);
        await db.SaveChangesAsync();

        var writer = new OutboxWriter(db);
        await writer.EnqueueAsync(new[] { "ref1" });

        Assert.AreEqual(2, db.OutboxMessages.Local.Count);
    }

    [TestMethod]
    public async Task EnqueueAsync_DoesNotCallSaveChanges()
    {
        using var db = CreateDbContext();
        var writer = new OutboxWriter(db);

        await writer.EnqueueAsync(new[] { "ref1" });

        // Message should be tracked but not persisted
        Assert.AreEqual(1, db.OutboxMessages.Local.Count);
        Assert.AreEqual(0, await db.OutboxMessages.CountAsync());
    }
}
