export interface ChatHistoryMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatRequest {
  message: string;
  history: ChatHistoryMessage[];
  context: {
    guestSessionId: string;
  };
}

export interface ChatToolCall {
  name: string;
  status: string;
}

export interface ChatResponse {
  message: string;
  toolCalls: ChatToolCall[];
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
}
