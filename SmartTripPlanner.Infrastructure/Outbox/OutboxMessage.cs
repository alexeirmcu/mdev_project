namespace SmartTripPlanner.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string PlaceProviderReferenceId { get; private set; }
    public string? PayloadJson { get; private set; }
    public OutboxMessageStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage()
    {
        PlaceProviderReferenceId = null!;
    }

    public static OutboxMessage Create(string placeProviderReferenceId, int maxRetries = 3)
    {
        var now = DateTime.UtcNow;
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            PlaceProviderReferenceId = placeProviderReferenceId,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            MaxRetries = maxRetries,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkProcessing()
    {
        Status = OutboxMessageStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = OutboxMessageStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ScheduleRetry()
    {
        var backoffSeconds = Math.Pow(2, RetryCount) * 30;
        NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
        RetryCount++;
        Status = OutboxMessageStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = OutboxMessageStatus.Failed;
        Error = error;
        NextAttemptAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reclaim()
    {
        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
