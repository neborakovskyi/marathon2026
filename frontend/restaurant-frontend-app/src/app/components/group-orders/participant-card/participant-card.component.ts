// Картка одного учасника — «дурний» компонент, тільки отримує дані через input().
// Практика: базова структура + title.
// Level 1: додаємо статус, страви, subtotal, стилі.
// Level 2: додаємо isOwn (My Pre-Order), canRemove, removeClick.
// Bonus:   canRemove + removeClick активні.
 
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Participant } from '@app/models/group-orders/participant.model';
 
@Component({
  selector: 'app-participant-card',
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [CurrencyPipe],   // потрібен для pipe currency в шаблоні
})
export class ParticipantCardComponent {
  // ─── inputs ───────────────────────────────────────────────────────────────
  /** Учасник — обов'язковий параметр */
  readonly participant = input.required<Participant>();
 
  /** true → показати «My Pre-Order» замість «Owner/Guest: Name» */
  readonly isOwn = input<boolean>(false);
 
  /** true → показати кнопку «Remove Guest» (тільки для гостей, до Submit) */
  readonly canRemove = input<boolean>(false);  // Бонус level-3
 
  // ─── output ───────────────────────────────────────────────────────────────
  /** Емітується при кліку «Remove Guest» — передає учасника наверх */
  readonly removeClick = output<Participant>();  // Бонус level-3
 
  // ─── computed ─────────────────────────────────────────────────────────────
  /**
   * Заголовок картки:
   *  - якщо це наша власна картка → «My Pre-Order»
   *  - інакше → «Owner: Name» або «Guest: Name» (тести практики)
   */
  readonly title = computed(() => {
    if (this.isOwn()) return 'My Pre-Order';
    const p = this.participant();
    return `${p.type}: ${p.name}`;
  });
 
  /**
   * Текстовий статус кошика — важливо: не тільки колір!
   * Тест level-1 перевіряє textContent: 'Submitted' | 'Still choosing'
   */
  readonly statusText = computed(() =>
    this.participant().isSubmitted ? 'Submitted' : 'Still choosing'
  );
 
  // ─── helper ───────────────────────────────────────────────────────────────
  /** Ціна × кількість для одного рядка страви */
  itemTotal(price: number, quantity: number): number {
    return price * quantity;
  }
}
