using Microsoft.Extensions.Options;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Infrastructure.LLM;

internal sealed class ConfigurationPromptTemplateProvider(IOptions<PromptTemplateOptions> options) : IPromptTemplateProvider
{
    public PromptTemplate GetTemplate(string name)
    {
        var templates = options.Value;
        return name switch
        {
            "PlaceEnrichment" => new PromptTemplate(
                templates.PlaceEnrichment.SystemPrompt,
                templates.PlaceEnrichment.UserPromptTemplate,
                templates.PlaceEnrichment.Temperature),
            _ => throw new ArgumentException($"Unknown template: {name}", nameof(name))
        };
    }
}
