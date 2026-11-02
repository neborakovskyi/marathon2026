import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatService } from '@app/services/chat/chat.service';
import { ChatComponent } from './chat.component';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  let chatService: {
    messages: ReturnType<typeof signal<{ role: 'user' | 'assistant'; text: string }[]>>;
    isLoading: ReturnType<typeof signal<boolean>>;
    error: ReturnType<typeof signal<string | null>>;
    sendMessage: jasmine.Spy;
  };

  beforeEach(() => {
    chatService = {
      messages: signal([]),
      isLoading: signal(false),
      error: signal(null),
      sendMessage: jasmine.createSpy('sendMessage').and.resolveTo(true),
    };

    TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [{ provide: ChatService, useValue: chatService }],
    });

    fixture = TestBed.createComponent(ChatComponent);
    fixture.detectChanges();
  });

  it('opens and closes the chat window from the toggle button', () => {
    const toggleButton = fixture.nativeElement.querySelector('.chat-toggle-btn') as HTMLButtonElement;

    expect(fixture.nativeElement.querySelector('.chat-container')).toBeNull();

    toggleButton.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.chat-container')).not.toBeNull();

    toggleButton.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.chat-container')).toBeNull();
  });

  it('renders messages, loading indicator, and service errors', () => {
    fixture.componentInstance.isOpen.set(true);
    chatService.messages.set([
      { role: 'user', text: 'What is on the menu?' },
      { role: 'assistant', text: 'Soup and salad.' },
    ]);
    chatService.isLoading.set(true);
    chatService.error.set('Chat is temporarily unavailable.');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.message-wrapper.user')?.textContent).toContain('What is on the menu?');
    expect(element.querySelector('.message-wrapper.assistant')?.textContent).toContain('Soup and salad.');
    expect(element.querySelector('.message-bubble.typing')).not.toBeNull();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Chat is temporarily unavailable.');
  });

  it('forwards input on Enter and clears it after a successful send', async () => {
    fixture.componentInstance.isOpen.set(true);
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = '  What is on the menu?  ';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter' }));
    await fixture.whenStable();

    expect(chatService.sendMessage).toHaveBeenCalledOnceWith('  What is on the menu?  ');
    expect(fixture.componentInstance.inputQuery).toBe('');
  });

  it('keeps the input after a failed send so the user can retry', async () => {
    chatService.sendMessage.and.resolveTo(false);
    fixture.componentInstance.isOpen.set(true);
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'What is on the menu?';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter' }));
    await fixture.whenStable();

    expect(fixture.componentInstance.inputQuery).toBe('What is on the menu?');
  });
});
