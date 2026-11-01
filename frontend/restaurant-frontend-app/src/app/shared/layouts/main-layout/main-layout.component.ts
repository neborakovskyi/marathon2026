import { Component, ChangeDetectionStrategy } from '@angular/core';
import { HeaderComponent } from '@app/shared/custom-components/header/header.component';
import { RouterOutlet } from '@angular/router';
import { ChatComponent } from '@app/components/chat/chat.component';

@Component({
  selector: 'app-main-layout',
  imports: [HeaderComponent, RouterOutlet, ChatComponent],
  templateUrl: './main-layout.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './main-layout.component.scss',
})
export class MainLayoutComponent {}
