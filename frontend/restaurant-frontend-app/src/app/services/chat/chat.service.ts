import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestPreorderStore } from '@app/pages/guest-pre-order-page/state/guest-preorder.store';
import { ChatHistoryMessage, ChatMessage, ChatRequest, ChatResponse } from './chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly guestPreorderStore = inject(GuestPreorderStore);

  readonly messages = signal<ChatMessage[]>([]);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  async sendMessage(userText: string): Promise<boolean> {
    const message = userText.trim();
    if (!message || this.isLoading()) return false;

    const guestSessionId = this.guestPreorderStore.guestSessionId();
    const guestSessionToken = this.guestPreorderStore.guestSessionToken();
    if (
      this.guestPreorderStore.status() !== 'active' ||
      !guestSessionId ||
      !guestSessionToken
    ) {
      this.error.set('Chat is available during an active guest pre-order session.');
      return false;
    }

    const history: ChatHistoryMessage[] = this.messages()
      .slice(-8)
      .map(({ role, text }) => ({ role, content: text }));
    const userTurn: ChatMessage = { role: 'user', text: message };
    this.messages.update(current => [...current, userTurn]);
    this.error.set(null);
    this.isLoading.set(true);

    const requestBody: ChatRequest = {
      message,
      history,
      context: { guestSessionId },
    };

    try {
      const response = await firstValueFrom(
        this.http.post<ChatResponse>(`${this.baseUrl}/chat`, requestBody, {
          headers: new HttpHeaders({ 'X-Guest-Session-Token': guestSessionToken }),
        }),
      );
      if (!response || typeof response.message !== 'string' || !response.message.trim()) {
        throw new Error('The chat service returned an empty response.');
      }

      this.messages.update(current => [...current, { role: 'assistant', text: response.message }]);
      return true;
    } catch (error: unknown) {
      this.messages.update(current => current.filter(turn => turn !== userTurn));
      this.error.set(this.getErrorMessage(error));
      return false;
    } finally {
      this.isLoading.set(false);
    }
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { error?: unknown; message?: unknown } | null;
      if (typeof body?.error === 'string') return body.error;
      if (typeof body?.message === 'string') return body.message;
      if (error.status === 0) return 'Could not reach the chat service. Please try again.';
    }

    if (error instanceof Error && error.message) return error.message;
    return 'The chat request failed. Please try again.';
  }
}
