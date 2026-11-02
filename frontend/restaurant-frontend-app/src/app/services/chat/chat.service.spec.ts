import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestPreorderStore } from '@app/pages/guest-pre-order-page/state/guest-preorder.store';
import { ChatService } from './chat.service';

describe('ChatService', () => {
  let service: ChatService;
  let httpMock: HttpTestingController;
  let sessionStatus: ReturnType<typeof signal<'active' | 'submitted'>>;
  let sessionId: ReturnType<typeof signal<string | null>>;
  let sessionToken: ReturnType<typeof signal<string | null>>;

  beforeEach(() => {
    sessionStatus = signal<'active' | 'submitted'>('active');
    sessionId = signal<string | null>('guest-123');
    sessionToken = signal<string | null>('private-token');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.example.test/develop' },
        {
          provide: GuestPreorderStore,
          useValue: {
            status: sessionStatus,
            guestSessionId: sessionId,
            guestSessionToken: sessionToken,
          },
        },
      ],
    });

    service = TestBed.inject(ChatService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the JSON request with the session token only in its dedicated header when guest session exists', async () => {
    const sending = service.sendMessage('  Show the menu  ');
    const request = httpMock.expectOne('https://api.example.test/develop/chat');

    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-Guest-Session-Token')).toBe('private-token');
    expect(request.request.body).toEqual({
      message: 'Show the menu',
      history: [],
      context: { guestSessionId: 'guest-123' },
    });
    expect(JSON.stringify(request.request.body)).not.toContain('private-token');
    expect(service.isLoading()).toBeTrue();

    request.flush({ message: 'Here is the menu.', toolCalls: [] });
    await sending;

    expect(service.messages()).toEqual([
      { role: 'user', text: 'Show the menu' },
      { role: 'assistant', text: 'Here is the menu.' },
    ]);
    expect(service.isLoading()).toBeFalse();
  });

  it('posts the JSON request without guest session token when no guest session exists', async () => {
    sessionId.set(null);
    sessionToken.set(null);

    const sending = service.sendMessage('  Show the menu  ');
    const request = httpMock.expectOne('https://api.example.test/develop/chat');

    expect(request.request.method).toBe('POST');
    expect(request.request.headers.has('X-Guest-Session-Token')).toBeFalse();
    expect(request.request.body).toEqual({
      message: 'Show the menu',
      history: [],
      context: { guestSessionId: '' },
    });
    expect(service.isLoading()).toBeTrue();

    request.flush({ message: 'Here is the menu.', toolCalls: [] });
    await sending;

    expect(service.messages()).toEqual([
      { role: 'user', text: 'Show the menu' },
      { role: 'assistant', text: 'Here is the menu.' },
    ]);
    expect(service.isLoading()).toBeFalse();
  });

  it('removes a failed user turn so retry history does not contain a duplicate', async () => {
    const sending = service.sendMessage('Show the menu');
    const request = httpMock.expectOne('https://api.example.test/develop/chat');

    request.flush(
      { error: 'The chat service is temporarily unavailable.' },
      { status: 502, statusText: 'Bad Gateway' },
    );
    expect(await sending).toBeFalse();

    expect(service.error()).toBe('The chat service is temporarily unavailable.');
    expect(service.isLoading()).toBeFalse();
    expect(service.messages()).toEqual([]);

    const retry = service.sendMessage('Show the menu');
    const retryRequest = httpMock.expectOne('https://api.example.test/develop/chat');
    expect(retryRequest.request.body.history).toEqual([]);
    retryRequest.flush({ message: 'Here is the menu.', toolCalls: [] });

    expect(await retry).toBeTrue();
    expect(service.messages()).toEqual([
      { role: 'user', text: 'Show the menu' },
      { role: 'assistant', text: 'Here is the menu.' },
    ]);
  });

  it('rejects an empty API response without adding an assistant message', async () => {
    const sending = service.sendMessage('Show the menu');
    const request = httpMock.expectOne('https://api.example.test/develop/chat');

    request.flush({ message: '  ', toolCalls: [] });
    await sending;

    expect(service.error()).toBe('The chat service returned an empty response.');
    expect(service.messages()).toEqual([]);
    expect(service.isLoading()).toBeFalse();
  });

  it('sends request even without guest session (anonymous user)', async () => {
    sessionStatus.set('submitted');
    sessionId.set(null);
    sessionToken.set(null);

    const sending = service.sendMessage('Show the menu');
    const request = httpMock.expectOne('https://api.example.test/develop/chat');

    expect(request.request.method).toBe('POST');
    expect(request.request.headers.has('X-Guest-Session-Token')).toBeFalse();
    expect(request.request.body).toEqual({
      message: 'Show the menu',
      history: [],
      context: { guestSessionId: '' },
    });

    request.flush({ message: 'Here is the menu.', toolCalls: [] });
    await sending;

    expect(service.messages()).toEqual([
      { role: 'user', text: 'Show the menu' },
      { role: 'assistant', text: 'Here is the menu.' },
    ]);
  });

  it('includes only the last eight messages in conversation history', async () => {
    service.messages.set(
      Array.from({ length: 10 }, (_, index) => ({
        role: index % 2 === 0 ? 'user' as const : 'assistant' as const,
        text: `message-${index}`,
      })),
    );
    const sending = service.sendMessage('Next question');
    const request = httpMock.expectOne('https://api.example.test/develop/chat');

    expect(request.request.body.history).toHaveSize(8);
    expect(request.request.body.history[0].content).toBe('message-2');
    request.flush({ message: 'Answer', toolCalls: [] });
    await sending;
  });
});
