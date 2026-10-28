import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupOrderPageComponent {
  readonly participants = signal(GROUP_ORDER_PREVIEW.sessions.participants);
}