using System.Collections.Generic;

namespace APIHandler.Models.Chat;

public sealed class ChatRequest
{
    public string? Message { get; init; }
    public List<ChatHistoryMessage>? History { get; init; }
    public ChatContext? Context { get; init; }
}

public sealed class ChatHistoryMessage
{
    public string? Role { get; init; }
    public string? Content { get; init; }
}

public sealed class ChatContext
{
    public string? GuestSessionId { get; init; }
}
