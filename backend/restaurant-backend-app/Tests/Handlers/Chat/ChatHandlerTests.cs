using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.BedrockRuntime;
using APIHandler.Handlers.Chat;
using APIHandler.Models.Chat;
using APIHandler.Services.Chat;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Text.Json;
using APIHandler.Services.Dishes;

namespace APIHandler.Tests.Handlers.Chat;

public class ChatHandlerTests
{
    private readonly Mock<IChatService> _chatService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public ChatHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_SessionTokenInBody_ReturnsBadRequestWithoutCallingService()
    {
        var handler = new ChatHandler(_chatService.Object);
        var request = new APIGatewayProxyRequest
        {
            Body = """
                {
                  "message": "Show the menu",
                  "context": { "guestSessionId": "guest-1" },
                  "guestSessionToken": "must-not-be-in-body"
                }
                """,
            Headers = new Dictionary<string, string>
            {
                ["X-Guest-Session-Token"] = "header-token"
            }
        };

        APIGatewayProxyResponse response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(400, response.StatusCode);
        _chatService.Verify(
            service => service.SendAsync(It.IsAny<ChatRequest>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_MissingSessionToken_AllowsAnonymousAccessAndCallsService()
    {
        _chatService
            .Setup(service => service.SendAsync(
                It.IsAny<ChatRequest>(),
                ""))
            .ReturnsAsync(new ChatResponse("Here is the menu.", []));
        var handler = new ChatHandler(_chatService.Object);
        var request = new APIGatewayProxyRequest
        {
            Body = """
                {
                  "message": "Show the menu",
                  "context": { "guestSessionId": "guest-1" }
                }
                """
        };

        APIGatewayProxyResponse response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(200, response.StatusCode);
        _chatService.Verify(
            service => service.SendAsync(
                It.Is<ChatRequest>(chatRequest =>
                    chatRequest.Context!.GuestSessionId == "guest-1" &&
                    chatRequest.Message == "Show the menu"),
                ""),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_UsesTokenHeaderAndReturnsJsonResponse()
    {
        _chatService
            .Setup(service => service.SendAsync(
                It.IsAny<ChatRequest>(),
                "header-token"))
            .ReturnsAsync(new ChatResponse("Here is the menu.", []));
        var handler = new ChatHandler(_chatService.Object);
        var request = new APIGatewayProxyRequest
        {
            Body = """
                {
                  "message": "Show the menu",
                  "history": [],
                  "context": { "guestSessionId": "guest-1" }
                }
                """,
            Headers = new Dictionary<string, string>
            {
                ["x-guest-session-token"] = "header-token"
            }
        };

        APIGatewayProxyResponse response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(200, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(response.Body);
        Assert.Equal("Here is the menu.", body.RootElement.GetProperty("message").GetString());
        _chatService.Verify(
            service => service.SendAsync(
                It.Is<ChatRequest>(chatRequest =>
                    chatRequest.Context!.GuestSessionId == "guest-1" &&
                    chatRequest.Message == "Show the menu"),
                "header-token"),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_ChatProviderFails_ReturnsBadGateway()
    {
        _chatService
            .Setup(service => service.SendAsync(
                It.IsAny<ChatRequest>(),
                It.IsAny<string>()))
            .ThrowsAsync(new ChatUpstreamException("provider failure"));
        var handler = new ChatHandler(_chatService.Object);
        var request = new APIGatewayProxyRequest
        {
            Body = """
                {
                  "message": "Show the menu",
                  "context": { "guestSessionId": "guest-1" }
                }
                """,
            Headers = new Dictionary<string, string>
            {
                ["X-Guest-Session-Token"] = "header-token"
            }
        };

        APIGatewayProxyResponse response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(502, response.StatusCode);
        _chatService.Verify(
            service => service.SendAsync(It.IsAny<ChatRequest>(), "header-token"),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_NullHistoryEntry_ReturnsBadRequest()
    {
        var bedrock = new Mock<IAmazonBedrockRuntime>();
        var guestSessions = new Mock<IGuestSessionService>();
        var dishService = new Mock<IDishService>();
        var handler = new ChatHandler(new BedrockChatService(bedrock.Object, guestSessions.Object, dishService.Object, "model-id"));
        var request = new APIGatewayProxyRequest
        {
            Body = """
                {
                  "message": "Show the menu",
                  "history": [null],
                  "context": { "guestSessionId": "guest-1" }
                }
                """,
            Headers = new Dictionary<string, string>
            {
                ["X-Guest-Session-Token"] = "header-token"
            }
        };

        APIGatewayProxyResponse response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(400, response.StatusCode);
        guestSessions.Verify(
            service => service.IsAuthorizedAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        bedrock.Verify(
            client => client.ConverseAsync(
                It.IsAny<Amazon.BedrockRuntime.Model.ConverseRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
