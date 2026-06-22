using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Infrastructure.Outbox;

internal sealed class OutboxWriter : IOutboxWriter
{
    private readonly PlannerDbContext _dbContext;

    public OutboxWriter(PlannerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(IEnumerable<string> placeProviderReferenceIds, CancellationToken ct = default)
    {
        var existingRefIds = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                || m.Status == OutboxMessageStatus.Processing)
            .Select(m => m.PlaceProviderReferenceId)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingRefIds);

        foreach (var refId in placeProviderReferenceIds)
        {
            if (!existingSet.Contains(refId))
            {
                var message = OutboxMessage.Create(refId);
                _dbContext.OutboxMessages.Add(message);
                existingSet.Add(refId);
            }
        }
    }
}
