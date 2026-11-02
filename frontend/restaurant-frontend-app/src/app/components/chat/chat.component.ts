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

  async send() {
    if (!this.inputQuery.trim()) return;
    if (await this.chatService.sendMessage(this.inputQuery)) {
      this.inputQuery = '';
    }
  }
}
