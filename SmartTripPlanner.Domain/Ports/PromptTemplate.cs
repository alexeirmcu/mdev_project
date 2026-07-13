namespace SmartTripPlanner.Domain.Ports;

public record PromptTemplate(string SystemPrompt, string UserPromptTemplate, float Temperature = 0.1f);
