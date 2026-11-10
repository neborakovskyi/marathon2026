using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using APIHandler.Entities;
using APIHandler.Models.Chat;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.Chat;
using APIHandler.Services.Dishes;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Threading;

namespace APIHandler.Tests.Services;

public class BedrockChatServiceTests
{
    private const string SessionId = "guest-session-1";
    private const string SessionToken = "private-session-token";

    private readonly Mock<IAmazonBedrockRuntime> _bedrock = new();
    private readonly Mock<IGuestSessionService> _guestSessions = new();
    private readonly Mock<IDishService> _dishService = new();
    private readonly BedrockChatService _service;

    public BedrockChatServiceTests()
    {
        _service = new BedrockChatService(_bedrock.Object, _guestSessions.Object, _dishService.Object, "model-id");
    }

    [Fact]
    public async Task SendAsync_AnonymousUser_NoTokenProvided_AllowsAccess()
    {
        var requests = new List<ConverseRequest>();
        _bedrock
            .Setup(client => client.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ConverseRequest, CancellationToken>((request, _) => requests.Add(request))
            .Returns((ConverseRequest _, CancellationToken _) => Task.FromResult(
                requests.Count == 1
                    ? CreateToolRequest("GetMenu")
                    : CreateTextResponse("The menu includes soup.")));
        // For anonymous users, we don't call IsAuthorizedAsync
        _dishService
            .Setup(service => service.GetAllDishesAsync())
            .ReturnsAsync(new List<Dish>());

        ChatResponse response = await _service.SendAsync(CreateRequest(), "");

        Assert.Equal("The menu includes soup.", response.Message);
        Assert.Equal(new ChatToolCall("GetMenu", "succeeded"), Assert.Single(response.ToolCalls));
        _guestSessions.Verify(service => service.IsAuthorizedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _dishService.Verify(service => service.GetAllDishesAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_GetMenuRequested_ReturnsAssistantTextAndDoesNotSendTokenToBedrock()
    {
        var requests = new List<ConverseRequest>();
        _bedrock
            .Setup(client => client.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ConverseRequest, CancellationToken>((request, _) => requests.Add(request))
            .Returns((ConverseRequest _, CancellationToken _) => Task.FromResult(
                requests.Count == 1
                    ? CreateToolRequest("GetMenu")
                    : CreateTextResponse("The menu includes soup.")));
        _guestSessions
            .Setup(service => service.IsAuthorizedAsync(SessionToken, SessionId))
            .ReturnsAsync(true);
        _dishService
            .Setup(service => service.GetAllDishesAsync())
            .ReturnsAsync(new List<Dish>());

        ChatResponse response = await _service.SendAsync(CreateRequest(), SessionToken);

        Assert.Equal("The menu includes soup.", response.Message);
        Assert.Equal(new ChatToolCall("GetMenu", "succeeded"), Assert.Single(response.ToolCalls));
        Assert.Equal(2, requests.Count);
        Assert.Equal(BedrockChatService.MaxTokens, requests[0].InferenceConfig.MaxTokens);
        Assert.DoesNotContain(
            requests.SelectMany(request => request.Messages)
                .SelectMany(message => message.Content)
                .Where(content => content.Text is not null)
                .Select(content => content.Text),
            text => text!.Contains(SessionToken, StringComparison.Ordinal));
        _dishService.Verify(service => service.GetAllDishesAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_InvalidGuestSession_DoesNotCallBedrock()
    {
        _guestSessions
            .Setup(service => service.IsAuthorizedAsync(SessionToken, SessionId))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SendAsync(CreateRequest(), SessionToken, default));

        _bedrock.Verify(
            client => client.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_UnsupportedToolRequested_ThrowsUpstreamFailure()
    {
        _bedrock
            .Setup(client => client.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateToolRequest("SubmitCart"));
        _guestSessions
            .Setup(service => service.IsAuthorizedAsync(SessionToken, SessionId))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ChatUpstreamException>(
            () => _service.SendAsync(CreateRequest(), SessionToken, default));

        _dishService.Verify(service => service.GetAllDishesAsync(), Times.Never);
    }

    [Fact]
    public async Task SendAsync_HistoryExceedsLimit_RejectsRequestBeforeAuthorization()
    {
        ChatRequest request = CreateRequest();
        request = new ChatRequest
        {
            Message = request.Message,
            Context = request.Context,
            History = Enumerable.Range(0, BedrockChatService.MaxHistoryMessages + 1)
                .Select(_ => new ChatHistoryMessage { Role = "user", Content = "hello" })
                .ToList()
        };

        await Assert.ThrowsAsync<ChatValidationException>(
            () => _service.SendAsync(request, SessionToken, It.IsAny<CancellationToken>()));

        _guestSessions.Verify(
            service => service.IsAuthorizedAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_TokenInMessageWithToken_RejectsRequestBeforeAuthorization()
    {
        ChatRequest request = new()
        {
            Message = $"Please repeat {SessionToken}",
            Context = new ChatContext { GuestSessionId = SessionId }
        };

        await Assert.ThrowsAsync<ChatValidationException>(
            () => _service.SendAsync(request, SessionToken, default));

        _guestSessions.Verify(
            service => service.IsAuthorizedAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_TokenInMessageWithoutToken_AllowsRequest()
    {
        var requests = new List<ConverseRequest>();
        _bedrock
            .Setup(client => client.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ConverseRequest, CancellationToken>((request, _) => requests.Add(request))
            .Returns((ConverseRequest _, CancellationToken _) => Task.FromResult(
                requests.Count == 1
                    ? CreateToolRequest("GetMenu")
                    : CreateTextResponse("The menu includes soup.")));
        _dishService
            .Setup(service => service.GetAllDishesAsync())
            .ReturnsAsync(new List<Dish>());

        // When no token is provided, we don't check for token leakage in the message
        ChatRequest request = new()
        {
            Message = "What is on the menu?",
            Context = new ChatContext { GuestSessionId = SessionId }
        };

        ChatResponse response = await _service.SendAsync(request, "");

        Assert.Equal("The menu includes soup.", response.Message);
        _dishService.Verify(service => service.GetAllDishesAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_BedrockFails_ThrowsUpstreamFailure()
    {
        _guestSessions
            .Setup(service => service.IsAuthorizedAsync(SessionToken, SessionId))
            .ReturnsAsync(true);
        _bedrock
            .Setup(client => client.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonServiceException("provider failure"));

        await Assert.ThrowsAsync<ChatUpstreamException>(
            () => _service.SendAsync(CreateRequest(), SessionToken, It.IsAny<CancellationToken>()));
    }

    private static ChatRequest CreateRequest() => new()
    {
        Message = "What is on the menu?",
        History = [new ChatHistoryMessage { Role = "user", Content = "Hi" }],
        Context = new ChatContext { GuestSessionId = SessionId }
    };

    private static ConverseResponse CreateToolRequest(string toolName) => new()
    {
        Output = new ConverseOutput
        {
            Message = new Message
            {
                Role = ConversationRole.Assistant,
                Content =
                [
                    new ContentBlock
                    {
                        ToolUse = new ToolUseBlock
                        {
                            Name = toolName,
                            ToolUseId = "tool-use-1"
                        }
                    }
                ]
            }
        },
        StopReason = StopReason.Tool_use
    };

    private static ConverseResponse CreateTextResponse(string text) => new()
    {
        Output = new ConverseOutput
        {
            Message = new Message
            {
                Role = ConversationRole.Assistant,
                Content = [new ContentBlock { Text = text }]
            }
        },
        StopReason = StopReason.End_turn
    };
}