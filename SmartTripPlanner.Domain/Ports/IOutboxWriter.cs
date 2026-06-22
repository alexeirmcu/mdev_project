namespace SmartTripPlanner.Domain.Ports;

public interface IOutboxWriter
{
    Task EnqueueAsync(IEnumerable<string> placeProviderReferenceIds, CancellationToken ct = default);
}
