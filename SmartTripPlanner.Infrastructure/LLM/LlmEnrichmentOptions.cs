namespace SmartTripPlanner.Infrastructure.LLM;

public class LlmEnrichmentOptions
{
    public const string SectionName = "LlmEnrichment";
    public bool UseFoursquarePremiumFields { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int LeaseTimeoutSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 10;
}
