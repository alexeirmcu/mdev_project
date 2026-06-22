namespace SmartTripPlanner.Infrastructure.LLM;

internal interface ILlmEnrichmentProcessor
{
    Task ProcessAsync(Guid messageId, CancellationToken ct = default);
}
