namespace SmartTripPlanner.Domain.Ports;

public interface ILlmClient
{
    Task<string> GetEnrichmentJsonAsync(string prompt, CancellationToken ct = default);
}
