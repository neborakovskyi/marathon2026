// ФАЙЛ: src/app/pages/group-order-page/group-order-page.component.ts
// Головна сторінка Owner Flow — читає reservationId з URL, завантажує дані,
// керує Submit і (level-3) фоновою синхронізацією.
 
import {
  ChangeDetectionStrategy, Component, computed,
  DestroyRef, inject, OnInit, signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute }    from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
 
import { EMPTY, forkJoin, interval } from 'rxjs';
import { catchError, exhaustMap, takeWhile } from 'rxjs/operators';
 
import { GroupOrdersService }    from '@app/services/group-orders/group-orders.service';
import { ToastService }          from '@app/services/toast/toast.service';
import { consolidatePreOrder }   from '@app/shared/helpers/consolidate-pre-order';
import { ModalComponent }        from '@app/shared/custom-components/modal/modal.component';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
 
import { GuestInvite }           from '@app/models/reservations/guest-invite.model';
import { Participant }           from '@app/models/group-orders/participant.model';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
 
/** Стани завантаження сторінки */
type LoadState = 'loading' | 'error' | 'loaded';
 
/** Інтервал фонового оновлення (мс) */
const POLL_INTERVAL_MS = 15_000;
 
@Component({
  selector: 'app-group-order-page',
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent, ModalComponent],
})
export class GroupOrderPageComponent implements OnInit {
  // ── DI ────────────────────────────────────────────────────────────────────
  private readonly route      = inject(ActivatedRoute);
  private readonly svc        = inject(GroupOrdersService);
  private readonly toastSvc   = inject(ToastService);
  /**
   * DestroyRef дозволяє takeUntilDestroyed() зупинити polling
   * коли користувач іде зі сторінки (ngOnDestroy).
   */
  private readonly destroyRef = inject(DestroyRef);
 
  // ── Ідентифікатори ────────────────────────────────────────────────────────
  private readonly reservationId = signal('');
  private readonly groupId       = signal('');
  private readonly ownerSessionId = signal('');
 
  // ── Стан завантаження ─────────────────────────────────────────────────────
  readonly loadState    = signal<LoadState>('loading');
  /** true — поки йде запит submit (блокує кнопку, запобігає подвійному кліку) */
  readonly submitLoading = signal(false);
 
  // ── Модальні вікна ────────────────────────────────────────────────────────
  readonly showSubmitModal = signal(false);
  /** Учасник, якого збираємося видалити (null → модал закритий) */
  readonly removingParticipant = signal<Participant | null>(null);
 
  // ── Дані ──────────────────────────────────────────────────────────────────
  readonly invite       = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly groupStatus  = signal<GroupReservationStatus>('Open');
 
  // ── Level-3: синхронізація ────────────────────────────────────────────────
  readonly lastUpdated  = signal<Date | null>(null);
  readonly refreshError = signal<string | null>(null);
 
  // ── Обчислювані значення ──────────────────────────────────────────────────
  /**
   * Консолідоване замовлення — лише підтверджені учасники.
   * Пеперераховується автоматично при кожній зміні participants().
   */
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
 
  /**
   * Grand Total = сума консолідованого (НЕ total з API).
   * Тест level-2: $540, а не $760 — бо Olena (isSubmitted=false) не враховується.
   */
  readonly grandTotal = computed(() => this.consolidated().total);
 
  /** Кількість учасників з непідтвердженим кошиком */
  readonly pendingCount = computed(() =>
    this.participants().filter(p => !p.isSubmitted).length
  );
 
  /** true коли статус не Open (readonly mode) */
  readonly isSubmitted = computed(() => this.groupStatus() !== 'Open');
 
  /** Текст бейджа статусу */
  readonly statusLabel = computed(() =>
    this.isSubmitted() ? 'Sent to Restaurant' : 'Collecting pre-orders'
  );
 
  /**
   * Кнопка Submit неактивна коли:
   * - запит вже йде (подвійний клік)
   * - немає підтверджених страв
   * - вже submitted
   */
  readonly submitDisabled = computed(() =>
    this.submitLoading() ||
    this.consolidated().items.length === 0 ||
    this.isSubmitted()
  );
 
  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    // Читаємо reservationId з URL: /reservations/:reservationId/group-order
    this.reservationId.set(
      this.route.snapshot.paramMap.get('reservationId') ?? ''
    );
    this.loadData();
  }
 
  // ── Завантаження даних ────────────────────────────────────────────────────
  loadData(): void {
    this.loadState.set('loading');
 
    // Крок 1: отримати invite → GroupReservationId
    this.svc.getGroupInvite$(this.reservationId()).subscribe({
      next: invite => {
        this.invite.set(invite);
        this.groupId.set(invite.GroupReservationId);
        this.loadSecondary(invite.GroupReservationId);
      },
      error: () => this.loadState.set('error'),
    });
  }
 
  private loadSecondary(groupId: string): void {
    // Крок 2: паралельно отримуємо sessions, status і ownerSessionId
    forkJoin({
      sessions:       this.svc.getSessions$(groupId),
      status:         this.svc.getStatus$(groupId),
      ownerSessionId: this.svc.getOwnerSessionId$(this.reservationId()),
    }).subscribe({
      next: ({ sessions, status, ownerSessionId }) => {
        this.participants.set(sessions.participants);
        this.groupStatus.set(status.status);
        this.ownerSessionId.set(ownerSessionId);
        this.lastUpdated.set(new Date());  // для «Updated at» (level-3)
        this.loadState.set('loaded');
        this.startPolling();  // запускаємо фонову синхронізацію (level-3)
      },
      error: () => this.loadState.set('error'),
    });
  }
 
  // ── Submit ────────────────────────────────────────────────────────────────
  openSubmitModal(): void  { this.showSubmitModal.set(true); }
  closeSubmitModal(): void { this.showSubmitModal.set(false); }
 
  confirmSubmit(): void {
    // Захист від подвійного кліку — кнопка стає 'Submitting…' і disabled
    if (this.submitLoading()) return;
    this.submitLoading.set(true);
    this.showSubmitModal.set(false);
 
    this.svc.submit$(this.groupId(), this.reservationId()).subscribe({
      next: () => {
        this.groupStatus.set('Submitted');
        this.submitLoading.set(false);
        // Toast про успіх — tokenInterceptor вже обробляє помилки,
        // тому success toast показуємо самі тільки тут
        this.toastSvc.success('Pre-order sent to the restaurant');
      },
      error: () => {
        this.submitLoading.set(false);
        // Помилку Submit показує tokenInterceptor (toast від API) — не дублюємо
      },
    });
  }
 
  // ── Remove Guest (Бонус level-3) ──────────────────────────────────────────
  openRemoveModal(participant: Participant): void {
    this.removingParticipant.set(participant);
  }
  closeRemoveModal(): void { this.removingParticipant.set(null); }
 
  confirmRemove(): void {
    const participant = this.removingParticipant();
    if (!participant) return;
    this.removingParticipant.set(null);
 
    this.svc.removeSession$(this.groupId(), participant.id).subscribe({
      // Відповідь DELETE містить оновлений список — computed() перерахується сам
      next: response => this.participants.set(response.participants),
      // При помилці список лишається без змін (тест bonus)
    });
  }
 
  // ── Level-3: фонова синхронізація ─────────────────────────────────────────
  /**
   * Запускається один раз після успішного завантаження.
   * takeUntilDestroyed → зупиняється коли компонент знищується.
   * takeWhile          → зупиняється після Submit (status !== 'Open').
   * exhaustMap         → якщо попередній запит ще не відповів, новий не стартує.
   * catchError всередині → помилка не завершує outer interval.
   */
  private startPolling(): void {
    interval(POLL_INTERVAL_MS).pipe(
      takeUntilDestroyed(this.destroyRef),
      takeWhile(() => this.groupStatus() === 'Open'),
      exhaustMap(() =>
        forkJoin({
          sessions: this.svc.getSessions$(this.groupId(), { silent: true }),
          status:   this.svc.getStatus$(this.groupId(),   { silent: true }),
        }).pipe(
          catchError(() => {
            // При помилці: зберігаємо поточні дані, показуємо повідомлення
            const time = this.lastUpdated()?.toLocaleTimeString() ?? '';
            this.refreshError.set(`Couldn't refresh. Showing data from ${time}`);
            return EMPTY; // EMPTY щоб не завершувати outer interval
          })
        )
      )
    ).subscribe(({ sessions, status }) => {
      this.participants.set(sessions.participants);
      this.groupStatus.set(status.status);
      this.lastUpdated.set(new Date());
      this.refreshError.set(null);
    });
  }
 
  /** Ручне оновлення (кнопка Refresh) — Level-3 */
  refresh(): void {
    if (this.groupStatus() !== 'Open') return;
 
    forkJoin({
      sessions: this.svc.getSessions$(this.groupId(), { silent: true }),
      status:   this.svc.getStatus$(this.groupId(),   { silent: true }),
    }).subscribe({
      next: ({ sessions, status }) => {
        this.participants.set(sessions.participants);
        this.groupStatus.set(status.status);
        this.lastUpdated.set(new Date());
        this.refreshError.set(null);
      },
      error: () => {
        const time = this.lastUpdated()?.toLocaleTimeString() ?? '';
        this.refreshError.set(`Couldn't refresh. Showing data from ${time}`);
      },
    });
  }
 
  /** Допоміжний computed — чи є картка власною (для шаблону) */
  isOwnCard(participantId: string): boolean {
    return participantId === this.ownerSessionId();
  }
 
  /** Чи можна видалити учасника */
  canRemove(participant: Participant): boolean {
    return participant.type === 'Guest' && !this.isSubmitted();
  }
}
