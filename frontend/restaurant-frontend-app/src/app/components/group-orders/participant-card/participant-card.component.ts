// ФАЙЛ: src/app/components/group-orders/participant-card/participant-card.component.ts
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [CurrencyPipe],
})
export class ParticipantCardComponent {
  /** Обов'язковий параметр — дані учасника */
  readonly participant = input.required<Participant>();

  /**
   * true → заголовок «My Pre-Order»
   * false (за замовч.) → «Owner: Name» або «Guest: Name»
   *
   * Тест практики: isOwn не передається → false
   *   render(OLENA) → 'Guest: Olena' ✓
   *   render(OWNER) → 'Owner: Yevhenii' ✓
   * Тест level-2: isOwn=true для owner-картки → 'My Pre-Order' ✓
   */
  readonly isOwn = input<boolean>(false);

  /** true → показати кнопку «Remove Guest» (Бонус level-3) */
  readonly canRemove = input<boolean>(false);

  /** Подія видалення — наверх йде сам учасник (Бонус level-3) */
  readonly removeClick = output<Participant>();

  readonly title = computed(() => {
    if (this.isOwn()) return 'My Pre-Order';
    const p = this.participant();
    // Тест: h2.textContent → 'Guest: Olena' або 'Owner: Yevhenii'
    return `${p.type}: ${p.name}`;
  });

  /** Текст статусу — НЕ лише колір (вимога a11y і тестів) */
  readonly statusText = computed(() =>
    this.participant().isSubmitted ? 'Submitted' : 'Still choosing'
  );

  /** Ціна рядка = price × quantity (тест: $440.00 = 220 × 2) */
  itemTotal(price: number, quantity: number): number {
    return price * quantity;
  }
}
