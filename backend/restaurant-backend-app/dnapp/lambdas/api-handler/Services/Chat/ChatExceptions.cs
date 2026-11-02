using System;

namespace APIHandler.Services.Chat;

public sealed class ChatValidationException(string message) : Exception(message);

public sealed class ChatUpstreamException(string message, Exception? innerException = null)
    : Exception(message, innerException);
