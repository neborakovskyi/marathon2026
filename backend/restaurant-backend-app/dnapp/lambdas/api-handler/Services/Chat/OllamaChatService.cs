using APIHandler.Models.Chat;
using APIHandler.Services.Dishes;
using APIHandler.Services.GuestSessions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace APIHandler.Services.Chat;

public sealed class OllamaChatService(
    IHttpClientFactory httpClientFactory,
    IDishService dishService,
    string apiKey,
    string modelName) : IChatService
{
    public const int MaxTokens = 512;
    public const int MaxMessageLength = 1000;
    public const int MaxHistoryMessages = 8;
    public const int MaxHistoryMessageLength = 2000;

    private const string GetMenuToolName = "GetMenu";

    private static readonly JsonSerializerOptions ToolJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ChatResponse> SendAsync(ChatRequest request, string guestSessionToken)
    {
        var messages = await CreateHistoryAsync(request);
        messages.Add(new OllamaMessage
        {
            Role = "user",
            Content = request.Message
        });

        var firstResponse = await CallOllamaAsync(messages);
        var toolCalls = firstResponse.Message.ToolCalls;

        if (toolCalls == null || toolCalls.Count == 0)
        {
            return new ChatResponse(firstResponse.Message.Content ?? string.Empty, []);
        }

        var toolCall = toolCalls[0];
        if (toolCall.Function.Name != GetMenuToolName)
        {
            throw new ChatUpstreamException($"Ollama requested an unsupported tool: {toolCall.Function.Name}");
        }

        var systemPrompt = await GetSystemPromptAsync();

        messages.Add(new OllamaMessage
        {
            Role = "assistant",
            Content = string.Empty,
            ToolCalls = toolCalls
        });
        messages.Add(new OllamaMessage
        {
            Role = "tool",
            Content = systemPrompt,
            ToolCallId = toolCall.Id
        });

        var finalResponse = await CallOllamaAsync(messages);
        return new ChatResponse(
            finalResponse.Message.Content ?? string.Empty,
            [new ChatToolCall(GetMenuToolName, "succeeded")]);
    }

    private async Task<string> GetSystemPromptAsync()
    {
        var menu = await dishService.GetAllDishesAsync();
        var toolResult = JsonSerializer.Serialize(menu, ToolJsonOptions);
        return $"""
            You are a friendly restaurant assistant. 
            Help guests explore the menu, get recommendations and descriptions of dishes.
            Here is the current menu (JSON):
            {toolResult}
            Rules:
            - Only describe dishes that exist in the menu above.
            - Do not invent dishes or prices.
            - Be concise and friendly.
            - Respond in the same language the guest uses.
            """;
    }

    private async Task<OllamaChatResponse> CallOllamaAsync(List<OllamaMessage> messages)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var request = new OllamaChatRequest
        {
            Model = modelName,
            Messages = messages,
            Tools = new List<OllamaTool>
            {
                new OllamaTool
                {
                    Type = "function",
                    Function = new OllamaFunction
                    {
                        Name = GetMenuToolName,
                        Description = "Read the menu available to the authorized guest session.",
                        Parameters = new OllamaParameters
                        {
                            Type = "object",
                            Properties = new Dictionary<string, object>(),
                            AdditionalProperties = false
                        }
                    }
                }
            },
            Stream = false
        };

        try
        {
            var json = JsonSerializer.Serialize(request, ToolJsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://ollama.com/api/chat", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Ollama Error] Status={(int)response.StatusCode}, Body={responseString}");
                throw new ChatUpstreamException("Ollama could not complete the chat request.");
            }

            return JsonSerializer.Deserialize<OllamaChatResponse>(responseString, ToolJsonOptions)
                ?? throw new ChatUpstreamException("Ollama returned an empty response.");
        }
        catch (HttpRequestException exception)
        {
            Console.WriteLine($"[Ollama Error] {exception.Message}");
            throw new ChatUpstreamException("Ollama could not complete the chat request.", exception);
        }
        catch (JsonException exception)
        {
            Console.WriteLine($"[Ollama Error] JSON parse error: {exception.Message}");
            throw new ChatUpstreamException("Ollama returned an invalid response.", exception);
        }
    }

    private async Task<List<OllamaMessage>> CreateHistoryAsync(ChatRequest request)
    {
        var messages = new List<OllamaMessage>();

        var systemPrompt = await GetSystemPromptAsync();
        

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new OllamaMessage { Role = "system", Content = systemPrompt });
        }

        foreach (var item in request.History ?? [])
        {
            messages.Add(new OllamaMessage
            {
                Role = item.Role == "user" ? "user" : "assistant",
                Content = item.Content
            });
        }

        return messages;
    }
}

// Ollama API DTOs
internal sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<OllamaTool> Tools { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

internal sealed class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public List<OllamaToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

internal sealed class OllamaToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OllamaFunctionCall Function { get; set; } = new();
}

internal sealed class OllamaFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "{}";
}

internal sealed class OllamaTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OllamaFunction Function { get; set; } = new();
}

internal sealed class OllamaFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public OllamaParameters Parameters { get; set; } = new();
}

internal sealed class OllamaParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("additionalProperties")]
    public bool AdditionalProperties { get; set; }
}

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public OllamaMessage Message { get; set; } = new();

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}