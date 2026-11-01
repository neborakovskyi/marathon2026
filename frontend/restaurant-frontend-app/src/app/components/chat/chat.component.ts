import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from '@app/services/chat/chat.service';

@Component({
  selector: 'app-ai-chat',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.css',
})

export class ChatComponent {
  readonly chatService = inject(ChatService);
  readonly isOpen = signal(false); // state open/close
  inputQuery = '';

  send() {
    if (!this.inputQuery.trim()) return;
    this.chatService.sendMessage(this.inputQuery);
    this.inputQuery = '';
  }
}
