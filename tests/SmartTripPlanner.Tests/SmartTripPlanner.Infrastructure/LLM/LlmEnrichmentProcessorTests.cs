using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;
using SmartTripPlanner.Infrastructure.LLM;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Tests.Infrastructure.LLM;

[TestClass]
public sealed class LlmEnrichmentProcessorTests
{
    private static PlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlannerDbContext(options);
    }

    private static Place CreatePlace(long id, string refId, bool isEnriched = false)
    {
        var place = new Place(refId, $"Place {refId}", 1L, new PlaceLocation(0, 0));
        typeof(Entity).GetField("_Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(place, id);
        if (isEnriched)
        {
            typeof(Place).GetProperty("IsEnriched")!.SetValue(place, true);
        }
        return place;
    }

    private static OutboxMessage CreateSeedMessage(Guid? id = null)
    {
        var msg = OutboxMessage.Create("fsq-test-place");
        if (id.HasValue)
        {
            typeof(OutboxMessage).GetProperty("Id")!.SetValue(msg, id.Value);
        }
        return msg;
    }

    [TestMethod]
    public async Task ProcessAsync_WithValidLlmResponse_EnrichesPlaceAndCompletes()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"TypicalDurationMinutes\":120,\"IsIndoor\":true,\"FamilyFriendlyScore\":4,\"Popularity\":0.8}");

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions());

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedPlace = await db.Places.FindAsync(place.Id);
        Assert.IsNotNull(updatedPlace);
        Assert.IsTrue(updatedPlace.IsEnriched);
        Assert.AreEqual(120, updatedPlace.TypicalDurationMinutes);
        Assert.IsTrue(updatedPlace.IsIndoor);
        Assert.AreEqual(4, updatedPlace.FamilyFriendlyScore);
        Assert.AreEqual(0.8, updatedPlace.Popularity);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(OutboxMessageStatus.Completed, updatedMessage.Status);
    }

    [TestMethod]
    public async Task ProcessAsync_WithInvalidJson_SchedulesRetry()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json");

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions());

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(OutboxMessageStatus.Pending, updatedMessage.Status);
        Assert.AreEqual(1, updatedMessage.RetryCount);
        Assert.IsNotNull(updatedMessage.NextAttemptAt);
    }

    [TestMethod]
    public async Task ProcessAsync_WithOutOfRangeValues_SchedulesRetry()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"TypicalDurationMinutes\":120,\"IsIndoor\":true,\"FamilyFriendlyScore\":7,\"Popularity\":0.8}");

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions { MaxRetries = 3 });

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(1, updatedMessage.RetryCount);
        Assert.AreEqual(OutboxMessageStatus.Pending, updatedMessage.Status);
    }

    [TestMethod]
    public async Task ProcessAsync_WithMaxRetriesExceeded_MarksFailed()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        // Set RetryCount to MaxRetries (3) by simulating prior retries
        typeof(OutboxMessage).GetProperty("RetryCount")!.SetValue(message, 3);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions { MaxRetries = 3 });

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(OutboxMessageStatus.Failed, updatedMessage.Status);
        Assert.IsNotNull(updatedMessage.Error);
    }

    [TestMethod]
    public async Task ProcessAsync_WithLlmExceptionAndRetryBelowMax_SchedulesRetry()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions());

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(OutboxMessageStatus.Pending, updatedMessage.Status);
        Assert.AreEqual(1, updatedMessage.RetryCount);
    }

    [TestMethod]
    public async Task ProcessAsync_PlaceNotFound_MarksFailed()
    {
        using var db = CreateDbContext();
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions());

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(OutboxMessageStatus.Failed, updatedMessage.Status);
        Assert.IsTrue(updatedMessage.Error!.Contains("not found"));
    }

    [TestMethod]
    public async Task ProcessAsync_MessageNotFound_DoesNotThrow()
    {
        using var db = CreateDbContext();
        var llmClientMock = new Mock<ILlmClient>();
        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions());

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        // Should not throw
        await processor.ProcessAsync(Guid.NewGuid());
    }

    [TestMethod]
    public async Task ProcessAsync_WithPremiumFieldsEnabled_FetchesFoursquareTips()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"TypicalDurationMinutes\":90,\"IsIndoor\":false,\"FamilyFriendlyScore\":3,\"Popularity\":0.6}");

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        fsqClientMock.Setup(f => f.GetPlaceByIdAsync("fsq-test-place", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoursquarePlace
            {
                FsqPlaceId = "fsq-test-place",
                Tips = new List<FoursquareTip>
                {
                    new() { Text = "Visit early to avoid crowds" }
                }
            });

        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions
        {
            UseFoursquarePremiumFields = true
        });

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
        Assert.IsNotNull(updatedMessage);
        Assert.AreEqual(OutboxMessageStatus.Completed, updatedMessage.Status);

        fsqClientMock.Verify(f => f.GetPlaceByIdAsync("fsq-test-place", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessAsync_WithPremiumFieldsDisabled_DoesNotFetchFoursquareTips()
    {
        using var db = CreateDbContext();
        var place = CreatePlace(1L, "fsq-test-place");
        db.Places.Add(place);
        var message = CreateSeedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(l => l.GetEnrichmentJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"TypicalDurationMinutes\":90,\"IsIndoor\":false,\"FamilyFriendlyScore\":3,\"Popularity\":0.6}");

        var fsqClientMock = new Mock<IFoursquareApiClient>();
        var optionsMock = new Mock<IOptions<LlmEnrichmentOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new LlmEnrichmentOptions
        {
            UseFoursquarePremiumFields = false
        });

        var processor = new LlmEnrichmentProcessor(
            db,
            llmClientMock.Object,
            fsqClientMock.Object,
            optionsMock.Object,
            Mock.Of<ILogger<LlmEnrichmentProcessor>>());

        await processor.ProcessAsync(message.Id);

        fsqClientMock.Verify(f => f.GetPlaceByIdAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
