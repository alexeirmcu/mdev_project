namespace SmartTripPlanner.Infrastructure.Outbox;

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
