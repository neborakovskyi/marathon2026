using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using APIHandler.Models.Chat;
using APIHandler.Services.Dishes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace APIHandler.Services.Chat;

public sealed class GroqChatService(
    IDishService dishService,
    string apiKey,
    string modelId = "llama-3.3-70b-versatile") : IChatService
{
    public const int MaxTokens = 512;
    public const int MaxHistoryMessages = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ChatResponse> SendAsync(ChatRequest request, string guestSessionToken)
    {
        // Меню как база знаний
        var menu = await dishService.GetAllDishesAsync();
        var menuJson = JsonSerializer.Serialize(menu, JsonOptions);

        var systemPrompt = $"""
            You are a friendly restaurant assistant. 
            Help guests explore the menu, get recommendations and descriptions of dishes.
            Here is the current menu (JSON):
            {menuJson}
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
                new ChatCompletionOptions { MaxOutputTokenCount = MaxTokens });

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