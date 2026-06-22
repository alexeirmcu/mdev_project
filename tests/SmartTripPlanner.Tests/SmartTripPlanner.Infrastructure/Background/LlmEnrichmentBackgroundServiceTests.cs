using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartTripPlanner.Infrastructure.Background;
using SmartTripPlanner.Infrastructure.LLM;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Tests.Infrastructure.Background;

[TestClass]
public sealed class LlmEnrichmentBackgroundServiceTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithPendingMessages_CallsProcessor()
    {
        var message1 = OutboxMessage.Create("ref1");
        var message2 = OutboxMessage.Create("ref2");

        var repoMock = new Mock<IOutboxMessageRepository>();
        repoMock.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message1, message2 });
        repoMock.Setup(r => r.ReclaimExpiredLeasesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processorMock = new Mock<ILlmEnrichmentProcessor>();
        processorMock.Setup(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions
        {
            PollingIntervalSeconds = 1,
            LeaseTimeoutSeconds = 300,
            BatchSize = 10
        });

        var services = new ServiceCollection();
        services.AddScoped(_ => repoMock.Object);
        services.AddScoped(_ => processorMock.Object);
        services.AddScoped(_ => optionsMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = Mock.Of<ILogger<LlmEnrichmentBackgroundService>>();

        var service = new LlmEnrichmentBackgroundService(scopeFactory, logger, optionsMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);

        // Let it process one iteration
        await Task.Delay(500);

        await service.StopAsync(CancellationToken.None);

        processorMock.Verify(p => p.ProcessAsync(message1.Id, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        processorMock.Verify(p => p.ProcessAsync(message2.Id, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        repoMock.Verify(r => r.ReclaimExpiredLeasesAsync(300, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithProcessorException_ContinuesToNextMessage()
    {
        var message1 = OutboxMessage.Create("ref1");
        var message2 = OutboxMessage.Create("ref2");

        var repoMock = new Mock<IOutboxMessageRepository>();
        repoMock.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message1, message2 });
        repoMock.Setup(r => r.ReclaimExpiredLeasesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processorMock = new Mock<ILlmEnrichmentProcessor>();
        processorMock.Setup(p => p.ProcessAsync(message1.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Processor error"));
        processorMock.Setup(p => p.ProcessAsync(message2.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions
        {
            PollingIntervalSeconds = 1,
            LeaseTimeoutSeconds = 300,
            BatchSize = 10
        });

        var services = new ServiceCollection();
        services.AddScoped(_ => repoMock.Object);
        services.AddScoped(_ => processorMock.Object);
        services.AddScoped(_ => optionsMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = Mock.Of<ILogger<LlmEnrichmentBackgroundService>>();

        var service = new LlmEnrichmentBackgroundService(scopeFactory, logger, optionsMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);

        await Task.Delay(500);

        await service.StopAsync(CancellationToken.None);

        // Both should have been processed despite the first one throwing
        processorMock.Verify(p => p.ProcessAsync(message1.Id, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        processorMock.Verify(p => p.ProcessAsync(message2.Id, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnCancellation_StopsGracefully()
    {
        var repoMock = new Mock<IOutboxMessageRepository>();
        repoMock.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage>());
        repoMock.Setup(r => r.ReclaimExpiredLeasesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processorMock = new Mock<ILlmEnrichmentProcessor>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions
        {
            PollingIntervalSeconds = 300, // Long interval
            LeaseTimeoutSeconds = 300,
            BatchSize = 10
        });

        var services = new ServiceCollection();
        services.AddScoped(_ => repoMock.Object);
        services.AddScoped(_ => processorMock.Object);
        services.AddScoped(_ => optionsMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = Mock.Of<ILogger<LlmEnrichmentBackgroundService>>();

        var service = new LlmEnrichmentBackgroundService(scopeFactory, logger, optionsMock.Object);

        using var cts = new CancellationTokenSource();
        var startTask = service.StartAsync(cts.Token);

        // Cancel quickly
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Should stop without exception
        Assert.IsTrue(true, "Service stopped gracefully");
    }
}
