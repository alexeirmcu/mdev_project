using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Infrastructure.LLM;

namespace SmartTripPlanner.Tests.Infrastructure.LLM;

[TestClass]
public sealed class LlmClientTests
{
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly Mock<ILogger<LlmClient>> _loggerMock = new();
    private readonly LlmApiOptions _options = new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://test.openai.com/v1/",
        Model = "test-model"
    };
    private readonly ILlmClient _client;

    public LlmClientTests()
    {
        var optionsMock = new Mock<IOptions<LlmApiOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        _client = new LlmClient(_chatClientMock.Object, optionsMock.Object, _loggerMock.Object);
    }

    [TestMethod]
    public async Task GetEnrichmentJsonAsync_WithValidResponse_ReturnsJsonString()
    {
        var expectedJson = "{\"TypicalDurationMinutes\":120}";
        var chatResponse = new ChatResponse(new List<ChatMessage>
        {
            new(ChatRole.Assistant, expectedJson)
        });

        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var result = await _client.GetEnrichmentJsonAsync("test prompt");

        Assert.AreEqual(expectedJson, result);
    }

    [TestMethod]
    public async Task GetEnrichmentJsonAsync_SendsSystemAndUserMessages()
    {
        var chatResponse = new ChatResponse(new List<ChatMessage>
        {
            new(ChatRole.Assistant, "{}")
        });

        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        await _client.GetEnrichmentJsonAsync("test prompt");

        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.Is<IList<ChatMessage>>(messages =>
                messages.Count == 2 &&
                messages[0].Role == ChatRole.System &&
                messages[1].Role == ChatRole.User &&
                messages[1].Text == "test prompt"),
            It.Is<ChatOptions>(o => o.ResponseFormat == ChatResponseFormat.Json),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetEnrichmentJsonAsync_WhenChatClientThrows_PropagatesException()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));

        try
        {
            await _client.GetEnrichmentJsonAsync("test prompt");
            Assert.Fail("Expected HttpRequestException was not thrown");
        }
        catch (HttpRequestException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task GetEnrichmentJsonAsync_WithEmptyResponse_ThrowsInvalidOperationException()
    {
        // Create a chat response with an empty messages list to simulate empty LLM response
        var chatResponse = new ChatResponse(new List<ChatMessage>());

        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        try
        {
            await _client.GetEnrichmentJsonAsync("test prompt");
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }
}
