using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTripPlanner.Infrastructure.LLM;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Infrastructure.Background;

internal sealed class LlmEnrichmentBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LlmEnrichmentBackgroundService> _logger;
    private readonly LlmEnrichmentOptions _options;

    public LlmEnrichmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LlmEnrichmentBackgroundService> logger,
        IOptions<LlmEnrichmentOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LlmEnrichmentBackgroundService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                var processor = scope.ServiceProvider.GetRequiredService<ILlmEnrichmentProcessor>();

                await repository.ReclaimExpiredLeasesAsync(_options.LeaseTimeoutSeconds, stoppingToken);
                var messages = await repository.GetPendingAsync(_options.BatchSize, stoppingToken);

                foreach (var message in messages)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await processor.ProcessAsync(message.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error processing outbox message {MessageId}, continuing loop", message.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LlmEnrichmentBackgroundService loop iteration");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("LlmEnrichmentBackgroundService stopped");
    }
}
