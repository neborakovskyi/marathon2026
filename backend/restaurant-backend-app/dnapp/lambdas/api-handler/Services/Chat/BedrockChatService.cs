using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.Runtime.Documents;
using APIHandler.Models.Chat;
using APIHandler.Services.GuestSessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using APIHandler.Services.Dishes;
using System.Threading;

namespace APIHandler.Services.Chat;

public sealed class BedrockChatService(
    IAmazonBedrockRuntime bedrockRuntime,
    IGuestSessionService guestSessionService,
    IDishService dishService,
    string modelId) : IChatService
{
    public const int MaxTokens = 512;
    public const int MaxMessageLength = 1000;
    public const int MaxHistoryMessages = 8;
    public const int MaxHistoryMessageLength = 2000;

    private const string GetMenuToolName = "GetMenu";
    private const string SystemPrompt =
        "You are a friendly restaurant assistant. Help guests with menu questions. " +
        "Use GetMenu when current menu information is needed. Only describe menu facts returned by that tool; " +
        "do not change carts, place orders, or claim to have made changes.";

    private static readonly JsonSerializerOptions ToolJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ChatResponse> SendAsync(ChatRequest request, string guestSessionToken, CancellationToken ct = default)
    {
        ValidateRequest(request, guestSessionToken);

        string guestSessionId = request.Context!.GuestSessionId!;
        bool authorized = true;

        // If token is provided, validate it; otherwise allow anonymous access
        if (!string.IsNullOrWhiteSpace(guestSessionToken))
        {
            try
            {
                authorized = await guestSessionService.IsAuthorizedAsync(guestSessionToken, guestSessionId);
            }
            catch (FormatException)
            {
                authorized = false;
            }

            if (!authorized)
                throw new UnauthorizedAccessException("The guest session token is invalid.");
        }

        var messages = CreateHistory(request);
        messages.Add(new Message
        {
            Role = ConversationRole.User,
            Content = [new ContentBlock { Text = request.Message }]
        });

        ConverseResponse response = await ConverseAsync(messages);
        var toolUses = response.Output.Message.Content
            .Where(block => block.ToolUse is not null)
            .Select(block => block.ToolUse!)
            .ToList();

        if (response.StopReason == StopReason.End_turn)
            return new ChatResponse(GetText(response.Output.Message), []);

        if (response.StopReason != StopReason.Tool_use)
            throw new ChatUpstreamException($"Unexpected stop reason: {response.StopReason}");

        if (toolUses.Count == 0)
            return new ChatResponse(GetText(response.Output.Message), []);

        if (toolUses.Count != 1 || toolUses[0].Name != GetMenuToolName)
            throw new ChatUpstreamException("Bedrock requested an unsupported tool.");

        var menu = await dishService.GetAllDishesAsync();
        var toolResult = new ToolResultBlock
        {
            ToolUseId = toolUses[0].ToolUseId,
            Content =
            [
                new ToolResultContentBlock
                {
                    Text = JsonSerializer.Serialize(menu, ToolJsonOptions)
                }
            ]
        };

        messages.Add(response.Output.Message);
        messages.Add(new Message
        {
            Role = ConversationRole.User,
            Content = [new ContentBlock { ToolResult = toolResult }]
        });

        ConverseResponse finalResponse = await ConverseAsync(messages);
        return new ChatResponse(
            GetText(finalResponse.Output.Message),
            [new ChatToolCall(GetMenuToolName, "succeeded")]);
    }

    private async Task<ConverseResponse> ConverseAsync(List<Message> messages)
    {
        try
        {
            return await bedrockRuntime.ConverseAsync(new ConverseRequest
            {
                ModelId = modelId,
                System = [new SystemContentBlock { Text = SystemPrompt }],
                Messages = messages,
                InferenceConfig = new InferenceConfiguration
                {
                    MaxTokens = MaxTokens,
                    Temperature = 0.3f
                },
                ToolConfig = new ToolConfiguration
                {
                    Tools =
                    [
                        new Tool
                        {
                            ToolSpec = new ToolSpecification
                            {
                                Name = GetMenuToolName,
                                Description = "Read the menu available to the authorized guest session.",
                                InputSchema = new ToolInputSchema
                                {
                                    Json = Document.FromObject(new
                                    {
                                        type = "object",
                                        properties = new { },
                                        additionalProperties = false
                                    })
                                }
                            }
                        }
                    ]
                }
            });
        }
        catch (AmazonServiceException exception)
        {
            Console.WriteLine($"[Bedrock Error] Code={exception.ErrorCode}, Status={exception.StatusCode}, Message={exception.Message}");
            throw new ChatUpstreamException("Bedrock could not complete the chat request.", exception);
        }
    }

    private static List<Message> CreateHistory(ChatRequest request)
    {
        var messages = new List<Message>();
        foreach (var item in request.History ?? [])
        {
            messages.Add(new Message
            {
                Role = item.Role == "user" ? ConversationRole.User : ConversationRole.Assistant,
                Content = [new ContentBlock { Text = item.Content }]
            });
        }

        return messages;
    }

    private static string GetText(Message message)
    {
        string text = string.Join(
            Environment.NewLine,
            message.Content.Where(block => !string.IsNullOrEmpty(block.Text)).Select(block => block.Text));
        if (string.IsNullOrWhiteSpace(text))
            throw new ChatUpstreamException("Bedrock returned an empty chat response.");

        return text;
    }

    private static void ValidateRequest(ChatRequest? request, string guestSessionToken)
    {
        if (request is null)
            throw new ChatValidationException("A JSON request body is required.");

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
            throw new ChatValidationException($"Message must contain 1 to {MaxMessageLength} characters.");

        // Allow empty guestSessionId for anonymous users
        // Only validate length if provided
        if (request.Context?.GuestSessionId != null && request.Context.GuestSessionId.Length > 128)
        {
            throw new ChatValidationException("Guest session ID must not exceed 128 characters.");
        }

        if (request.History is { Count: > MaxHistoryMessages })
            throw new ChatValidationException($"History may contain at most {MaxHistoryMessages} messages.");

        foreach (var item in request.History ?? [])
        {
            if (item is null)
                throw new ChatValidationException("History messages must be objects.");

            if (item.Role is not ("user" or "assistant"))
                throw new ChatValidationException("History roles must be 'user' or 'assistant'.");

            if (string.IsNullOrWhiteSpace(item.Content) || item.Content.Length > MaxHistoryMessageLength)
                throw new ChatValidationException(
                    $"History messages must contain 1 to {MaxHistoryMessageLength} characters.");
        }

        // Only check for token leakage if token is provided
        if (!string.IsNullOrWhiteSpace(guestSessionToken))
        {
            if ((request.Context?.GuestSessionId?.Contains(guestSessionToken, StringComparison.Ordinal) ?? false) ||
                request.Message.Contains(guestSessionToken, StringComparison.Ordinal) ||
                (request.History ?? []).Any(item =>
                    item.Content!.Contains(guestSessionToken, StringComparison.Ordinal)))
            {
                throw new ChatValidationException("The guest session token must be sent only in its request header.");
            }
        }
    }
}
