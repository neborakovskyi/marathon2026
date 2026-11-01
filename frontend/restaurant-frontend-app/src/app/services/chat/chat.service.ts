import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ChatService {
  readonly messages = signal<{ role: 'user' | 'assistant'; text: string }[]>([]);
  readonly isStreaming = signal(false);

  async sendMessage(userText: string) {
    // 1. Обновляем UI сообщением пользователя
    this.messages.update(prev => [...prev, { role: 'user', text: userText }]);
    this.isStreaming.set(true);

    // Добавляем пустой шаблон для ответа ИИ, который будем наполнять чанками
    this.messages.update(prev => [...prev, { role: 'assistant', text: '' }]);

    try {
      const response = await fetch('https://amazonaws.com', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: userText })
      });

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();
      if (!reader) return;

      while (true) {
        const { done, value } = await reader.read(); // Читаем поток по кусочкам
        if (done) break;

        const chunk = decoder.decode(value);

        // Дописываем пришедший текст в последнее сообщение
        this.messages.update(prev => {
          const updated = [...prev];
          const last = updated[updated.length - 1];
          if (last && last.role === 'assistant') {
            last.text += chunk;
          }
          return updated;
        });
      }
    } finally {
      this.isStreaming.set(false);
    }
  }
}
