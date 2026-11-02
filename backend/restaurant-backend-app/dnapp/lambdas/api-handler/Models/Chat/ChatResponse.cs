using System.Collections.Generic;

namespace APIHandler.Models.Chat;

public sealed record ChatResponse(string Message, IReadOnlyList<ChatToolCall> ToolCalls);

public sealed record ChatToolCall(string Name, string Status);
