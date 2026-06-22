namespace SmartTripPlanner.Infrastructure.Outbox;

internal interface IOutboxMessageRepository
{
    Task<List<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct);
    Task ReclaimExpiredLeasesAsync(int leaseTimeoutSeconds, CancellationToken ct);
}
