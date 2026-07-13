using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Infrastructure.LLM;

internal sealed class LlmEnrichmentProcessor : ILlmEnrichmentProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly PlannerDbContext _dbContext;
    private readonly ILlmClient _llmClient;
    private readonly IFoursquareApiClient _foursquareApiClient;
    private readonly IPromptTemplateProvider _templateProvider;
    private readonly PlaceEnrichmentPromptBuilder _promptBuilder;
    private readonly LlmEnrichmentOptions _options;
    private readonly ILogger<LlmEnrichmentProcessor> _logger;

    public LlmEnrichmentProcessor(
        PlannerDbContext dbContext,
        ILlmClient llmClient,
        IFoursquareApiClient foursquareApiClient,
        IPromptTemplateProvider templateProvider,
        PlaceEnrichmentPromptBuilder promptBuilder,
        IOptions<LlmEnrichmentOptions> options,
        ILogger<LlmEnrichmentProcessor> logger)
    {
        _dbContext = dbContext;
        _llmClient = llmClient;
        _foursquareApiClient = foursquareApiClient;
        _templateProvider = templateProvider;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (message is null)
        {
            _logger.LogWarning("Outbox message {MessageId} not found", messageId);
            return;
        }

        message.MarkProcessing();
        await _dbContext.SaveChangesAsync(ct);

        var place = await _dbContext.Places
            .Include(p => p.OpeningHours)
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.ProviderReferenceId == message.PlaceProviderReferenceId, ct);

        if (place is null)
        {
            _logger.LogWarning("Place {ProviderReferenceId} not found for message {MessageId}",
                message.PlaceProviderReferenceId, messageId);
            message.MarkFailed("Place not found");
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        try
        {
            string? tipsText = null;
            if (_options.UseFoursquarePremiumFields)
            {
                var foursquarePlace = await _foursquareApiClient.GetPlaceByIdAsync(
                    place.ProviderReferenceId, includeTips: true, ct);
                if (foursquarePlace?.Tips is { Count: > 0 })
                {
                    tipsText = string.Join(" | ", foursquarePlace.Tips.Select(t => t.Text));
                }
            }

            var template = _templateProvider.GetTemplate("PlaceEnrichment");
            var prompt = _promptBuilder.Build(place, tipsText);
            var json = await _llmClient.GetEnrichmentJsonAsync(template.SystemPrompt, prompt, template.Temperature, ct);

            var response = JsonSerializer.Deserialize<PlaceEnrichmentResponse>(json, JsonOptions);
            if (response is null)
                throw new InvalidOperationException("LLM returned null JSON");

            response.Validate();

            place.MarkEnriched(
                response.TypicalDurationMinutes,
                response.IsIndoor,
                response.FamilyFriendlyScore,
                response.Popularity);

            message.MarkCompleted();
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Enriched place {PlaceId} ({Name}) from message {MessageId}",
                place.Id, place.Name, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich place {ProviderReferenceId} from message {MessageId}",
                message.PlaceProviderReferenceId, messageId);

            if (message.RetryCount >= message.MaxRetries)
            {
                message.MarkFailed(ex.Message);
            }
            else
            {
                message.ScheduleRetry();
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
