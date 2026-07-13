namespace SmartTripPlanner.Domain.Ports;

public interface ILlmClient
{
    Task<string> GetEnrichmentJsonAsync(string systemPrompt, string userPrompt, float temperature, CancellationToken ct = default);
}
