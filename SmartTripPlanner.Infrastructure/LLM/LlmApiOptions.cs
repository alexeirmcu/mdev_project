namespace SmartTripPlanner.Infrastructure.LLM;

public class LlmApiOptions
{
    public const string SectionName = "LlmApi";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string Model { get; set; } = "gpt-4o-mini";
}
