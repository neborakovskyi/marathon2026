using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Helpers;
using APIHandler.Models.Chat;
using APIHandler.Services.Chat;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace APIHandler.Handlers.Chat;

public sealed class ChatHandler(IChatService chatService) : IApiRequestHandler
{
    private const string GuestSessionTokenHeader = "X-Guest-Session-Token";

    public async Task<APIGatewayProxyResponse> HandleRequest(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        ChatRequest? chatRequest;
        
        var timeout = context.RemainingTime - TimeSpan.FromSeconds(3);
        using var cts = new CancellationTokenSource(timeout > TimeSpan.Zero ? timeout : TimeSpan.FromMilliseconds(1));
        CancellationToken ct = cts.Token;

        if (string.IsNullOrWhiteSpace(request.Body))
            return ResponseBuilder.CreateErrorResponse(400, "A JSON request body is required.", context.Logger);

        try
        {
            chatRequest = JsonSerializer.Deserialize<ChatRequest>(
                request.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                });
        }
        catch (JsonException)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Invalid JSON request body.", context.Logger);
        }

        string? token = request.Headers?
            .FirstOrDefault(header => string.Equals(
                header.Key,
                GuestSessionTokenHeader,
                StringComparison.OrdinalIgnoreCase))
            .Value;

        // Token is now optional - pass null/empty if not provided
        try
        {
            ChatResponse result = await chatService.SendAsync(chatRequest!, token ?? string.Empty, ct);
            return ResponseBuilder.CreateJsonResponse(200, result);
        }
        catch (ChatValidationException exception)
        {
            return ResponseBuilder.CreateErrorResponse(400, exception.Message, context.Logger);
        }
        catch (UnauthorizedAccessException)
        {
            return ResponseBuilder.CreateErrorResponse(401, "Invalid guest session token.", context.Logger);
        }
        catch (ChatUpstreamException exception)
        {
            context.Logger.LogLine($"Chat provider failure ({exception.GetType().Name}).");
            return ResponseBuilder.CreateErrorResponse(502, "The chat service is temporarily unavailable.", context.Logger);
        }
    }
}
