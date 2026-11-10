using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using APIHandler.Models.Chat;
using APIHandler.Services.Dishes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using APIHandler.Services.Locations;
using APIHandler.Services.Tables;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;

namespace APIHandler.Services.Chat;

public sealed class GroqChatService(
    IDishService dishService,
    ILocationService locationService,
    ITableService tableService,
    IMemoryCache memoryCache,
    string apiKey,
    string modelId = "llama-3.3-70b-versatile") : IChatService
{
    public const int MaxTokens = 512;
    public const int MaxHistoryMessages = 8;

    public const int CACHE_EXPIRATION_DAYS = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ChatResponse> SendAsync(ChatRequest request, string guestSessionToken, CancellationToken ct = default)
    {
        // Create knowledge base - try cache first, fallback to service calls
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(CACHE_EXPIRATION_DAYS)
        };

        var dishesTask = memoryCache.GetOrCreateAsync("chat:dishes", async entry =>
        {
            entry.SetOptions(cacheOptions);
            return await dishService.GetAllDishesAsync();
        });

        var locationsTask = memoryCache.GetOrCreateAsync("chat:locations", async entry =>
        {
            entry.SetOptions(cacheOptions);
            return await locationService.GetAllLocationsAsync();
        });

        var tablesTask = memoryCache.GetOrCreateAsync("chat:tables", async entry =>
        {
            entry.SetOptions(cacheOptions);
            return await tableService.GetAllTablesAsync();
        });

        await Task.WhenAll(dishesTask, locationsTask, tablesTask);

        var dishesJson = JsonSerializer.Serialize(await dishesTask, JsonOptions);
        var locationsJson = JsonSerializer.Serialize(await locationsTask, JsonOptions);
        var tablesJson = JsonSerializer.Serialize(await tablesTask, JsonOptions);

        var systemPrompt = $"""
            You are a friendly restaurant assistant. 
            Help guests explore the menu, get recommendations and descriptions of dishes.
            Here is the dishes (JSON):
            {dishesJson}
            Here is the locations (JSON):
            {locationsJson}
            Here is the tables (JSON):
            {tablesJson}
            Rules:
            - Only describe dishes that exist in the menu above.
            - Do not invent dishes or prices.
            - Be concise and friendly.
            - Respond in the same language the guest uses.
            """;

        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var item in request.History ?? [])
        {
            messages.Add(item.Role == "user"
                ? new UserChatMessage(item.Content)
                : (ChatMessage)new AssistantChatMessage(item.Content));
        }

        messages.Add(new UserChatMessage(request.Message));

        try
        {
            var client = new ChatClient(
                modelId,
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") });

            var completion = await client.CompleteChatAsync(messages,
                new ChatCompletionOptions {
                    Temperature = .7f, 
                    MaxOutputTokenCount = MaxTokens 
                }, ct);

            var text = completion.Value.Content[0].Text; //Streaming OFF

            Console.WriteLine($"Groq response: {text}");

            if (string.IsNullOrWhiteSpace(text))
                throw new ChatUpstreamException("Groq returned an empty response.");

            return new ChatResponse(text, []);
        }
        catch (ClientResultException ex)
        {
            Console.WriteLine($"Groq API error: {ex.Message}");
            throw new ChatUpstreamException("Groq could not complete the request.", ex);
        }
    }
}