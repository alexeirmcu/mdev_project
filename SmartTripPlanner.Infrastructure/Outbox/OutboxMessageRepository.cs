using Microsoft.EntityFrameworkCore;

namespace SmartTripPlanner.Infrastructure.Outbox;

internal sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly PlannerDbContext _dbContext;

    public OutboxMessageRepository(PlannerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow;
        return await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                && (m.NextAttemptAt == null || m.NextAttemptAt <= cutoff))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task ReclaimExpiredLeasesAsync(int leaseTimeoutSeconds, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-leaseTimeoutSeconds);
        var stuckMessages = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processing
                && m.UpdatedAt <= cutoff)
            .ToListAsync(ct);

        foreach (var message in stuckMessages)
        {
            message.Reclaim();
        }
    }
}
