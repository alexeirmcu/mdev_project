namespace SmartTripPlanner.Infrastructure.LLM;

public class PromptTemplateOptions
{
    public const string SectionName = "PromptTemplates";
    public PlaceEnrichmentTemplateConfig PlaceEnrichment { get; set; } = new();
}

public class PlaceEnrichmentTemplateConfig
{
    public string SystemPrompt { get; set; } = "You are a place metadata assistant. Respond ONLY with valid JSON.";
    public string UserPromptTemplate { get; set; } = "Place: {{Name}}\n{{CategoriesSection}}{{OpeningHoursSection}}{{VisitorTipsSection}}\n\nRespond with valid JSON only in this exact schema:\n{{Schema}}";
    public float Temperature { get; set; } = 0.1f;
}
