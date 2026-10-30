// ФАЙЛ: src/app/pages/group-order-page/group-order-page.component.ts
// Єдиний файл для Level 1→3 + Бонус. Замінюй повністю при переході на Level 2.
import {
  ChangeDetectionStrategy, Component, computed,
  DestroyRef, inject, OnInit, signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { EMPTY, forkJoin, interval } from 'rxjs';
import { catchError, exhaustMap, takeWhile } from 'rxjs/operators';

import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { AuthenticationService } from '@app/auth/services/authentication.service';

import { MAT_DIALOG_DATA } from '@angular/material/dialog';

type LoadState = 'loading' | 'error' | 'loaded';

/** 15 секунд — константа (тест використовує tick(15_000)) */
const POLL_MS = 15_000;

@Component({
  selector: 'app-group-order-page',
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent, ModalComponent],
})
export class GroupOrderPageComponent implements OnInit {
  private readonly dialogData = inject<{ reservationId?: string } | null>(
    MAT_DIALOG_DATA,
    { optional: true },
  );

  // ── DI ──────────────────────────────────────────────────────────────────────
  private readonly route = inject(ActivatedRoute);
  private readonly svc = inject(GroupOrdersService);
  private readonly toastSvc = inject(ToastService);

  private readonly router = inject(Router);
  private readonly authenticationService = inject(AuthenticationService);
  /**
   * DestroyRef → takeUntilDestroyed() зупиняє polling при виході зі сторінки.
   * Тест level-3: fixture.destroy() → tick(45s) → getSessions$ 1 раз (тільки initial)
   */
  private readonly destroyRef = inject(DestroyRef);

  // ── IDs ─────────────────────────────────────────────────────────────────────
  private readonly reservationId = signal('');
  private readonly groupId = signal('');
  private readonly ownerSessionId = signal('');

  // ── UI state ────────────────────────────────────────────────────────────────
  readonly loadState = signal<LoadState>('loading');
  /** true → кнопка Submit стає 'Submitting…', disabled */
  readonly submitLoading = signal(false);

  // ── Modals ──────────────────────────────────────────────────────────────────
  /** true → відображається <app-modal role="dialog"> для Submit */
  readonly showSubmitModal = signal(false);
  /** не null → відображається <app-modal> для Remove Guest (Бонус) */
  readonly removingParticipant = signal<Participant | null>(null);

  // ── Data ────────────────────────────────────────────────────────────────────
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly groupStatus = signal<GroupReservationStatus>('Open');

  // ── Level-3 sync ────────────────────────────────────────────────────────────
  readonly lastUpdated = signal<Date | null>(null);
  readonly refreshError = signal<string | null>(null);

  // ── Computed ────────────────────────────────────────────────────────────────
  /**
   * Консолідоване замовлення — тільки isSubmitted=true учасники.
   * Перераховується автоматично при кожному participants.set(...)
   *
   * Тест level-2: 'Caesar Salad × 3' (owner ×1 + andrii ×2, olena не враховується)
   */
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));

  /**
   * Grand Total = сума консолідованого (НЕ 'total' з API-відповіді).
   * КЛЮЧОВА ПАСТКА: API повертає total=760, але тест очікує $540.00
   * (olena isSubmitted=false → її страви не рахуємо)
   */
  readonly grandTotal = computed(() => this.consolidated().total);

  /** Кількість учасників з непідтвердженим кошиком */
  readonly pendingCount = computed(() =>
    //this.participants().filter(p => !p.isSubmitted).length
  {console.log(this.participants());
    return 5;
  }
  );

  /** true → Submitted або Closed (read-only режим, немає кнопки Submit) */
  readonly isSubmitted = computed(() => this.groupStatus() !== 'Open');

  /** Текст бейджа поруч з h1 */
  readonly statusLabel = computed(() =>
    this.isSubmitted() ? 'Sent to Restaurant' : 'Collecting pre-orders'
  );

  /**
   * Кнопка Submit неактивна коли:
   * - запит вже іде (submitLoading=true → захист від подвійного кліку)
   * - немає підтверджених страв
   * - статус вже Submitted
   *
   * Тест: Submit неактивна коли isSubmitted=false для всіх → disabled=true
   */
  readonly submitDisabled = computed(() =>
    this.submitLoading() ||
    this.consolidated().items.length === 0 ||
    this.isSubmitted()
  );

  /**
   * ВИПРАВЛЕННЯ: true якщо хоч у когось є страви.
   * Тест level-2 «порожньо»: empty.participants.forEach(p => p.items = [])
   * → всі participants є, але items порожні → hasAnyDishes=false
   * → показуємо "Participants haven't added any dishes yet"
   */
  readonly hasAnyDishes = computed(() =>
    this.participants().some(p => p.items.length > 0)
  );

  //Token
  private token?: string = "";

  // ── Lifecycle ────────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.token = this.authenticationService.getToken();
    const user = this.authenticationService.getUser();

    if (!this.token || !user) {
      void this.router.navigate(['/login']);
      return;
    }
    // reservationId береться з URL: /reservations/:reservationId/group-order
    this.reservationId.set(
      this.dialogData?.reservationId ??
        this.route.snapshot.paramMap.get('reservationId') ??
        '',
    );
    this.loadData();
  }

  // ── Data loading ─────────────────────────────────────────────────────────────
  loadData(): void {
    this.loadState.set('loading');

    // Крок 1: invite-token → invite (містить GroupReservationId)
    this.svc.getGroupInvite$(this.reservationId()).subscribe({
      next: invite => {
        this.invite.set(invite);
        this.groupId.set(invite.GroupReservationId);
        this.loadSecondary(invite.GroupReservationId);
      },
      // Тест: 404 → loadState='error' → textContent.toContain('Group reservation not found')
      error: () => this.loadState.set('error'),
    });
  }

  private loadSecondary(groupId: string): void {
    // Паралельно: sessions + status + ownerSessionId
    forkJoin({
      sessions: this.svc.getSessions$(groupId, this.token),
      status: this.svc.getStatus$(groupId, this.token),
      ownerSessionId: this.svc.getOwnerSessionId$(this.reservationId()),
    }).subscribe({
      next: ({ sessions, status, ownerSessionId }) => {
        this.participants.set(sessions.participants);
        this.groupStatus.set(status.status);
        this.ownerSessionId.set(ownerSessionId);
        // Level-3: "Updated at …" з'являється одразу після initial load
        this.lastUpdated.set(new Date());
        this.loadState.set('loaded');
        // Level-3: запускаємо polling після успішного завантаження
        this.startPolling();
      },
      error: () => this.loadState.set('error'),
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────────
  openSubmitModal(): void { this.showSubmitModal.set(true); }
  closeSubmitModal(): void { this.showSubmitModal.set(false); }

  confirmSubmit(): void {
    // Захист від повторного виклику — submitLoading блокує кнопку
    if (this.submitLoading()) return;
    this.submitLoading.set(true);
    // Закриваємо модал одразу — DOM оновиться після detectChanges()
    // Тест: dialog стає detached → buttonByText(dialog, 'Submit')?.click() → undefined → нічого
    this.showSubmitModal.set(false);

    this.svc.submit$(this.groupId(), this.reservationId()).subscribe({
      next: () => {
        this.groupStatus.set('Submitted'); // бейдж → 'Sent to Restaurant', Submit кнопка зникає
        this.submitLoading.set(false);
        this.toastSvc.success('Pre-order sent to the restaurant');
      },
      // Тест: помилка → submitLoading=false → кнопку можна натиснути знову
      // Toast з текстом API показує tokenInterceptor — не дублюємо
      error: () => { this.submitLoading.set(false); },
    });
  }

  // ── Remove Guest (Бонус) ──────────────────────────────────────────────────────
  openRemoveModal(participant: Participant): void { this.removingParticipant.set(participant); }
  closeRemoveModal(): void { this.removingParticipant.set(null); }

  confirmRemove(): void {
    const participant = this.removingParticipant();
    if (!participant) return;
    this.removingParticipant.set(null); // закриваємо модал

    this.svc.removeSession$(this.groupId(), participant.id, "").subscribe({
      // Відповідь DELETE = оновлений список → computed() перерахується
      next: response => this.participants.set(response.participants),
      // Тест: помилка → participants не змінюються (список залишається)
    });
  }

  // ── Level-3: polling ──────────────────────────────────────────────────────────
  /**
   * interval(15_000) запускається ПІСЛЯ initial load.
   *
   * takeUntilDestroyed(destroyRef):
   *   Тест: fixture.destroy() → polling зупиняється → tick(45s) → getSessions$ 1 раз
   *
   * takeWhile(() => status === 'Open'):
   *   Тест: після Submit, tick(45s) → getSessions$ 1 раз (initial load)
   *
   * exhaustMap:
   *   Якщо попередній запит ще не відповів, новий не стартує
   *
   * catchError inside pipe → EMPTY:
   *   Помилка не завершує outer interval (тест: Couldn't refresh, але список лишається)
   */
  private startPolling(): void {
    interval(POLL_MS).pipe(
      takeUntilDestroyed(this.destroyRef),
      takeWhile(() => this.groupStatus() === 'Open'),
      exhaustMap(() =>
        forkJoin({
          sessions: this.svc.getSessions$(this.groupId(), this.token, { silent: true }),
          status: this.svc.getStatus$(this.groupId(), this.token, { silent: true }),
        }).pipe(
          catchError(() => {
            // Зберігаємо поточні дані, показуємо повідомлення про помилку
            const time = this.lastUpdated()?.toLocaleTimeString() ?? '';
            this.refreshError.set(`Couldn't refresh. Showing data from ${time}`);
            return EMPTY; // EMPTY → outer interval продовжує роботу
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

  /** Ручне оновлення (кнопка «Refresh», Level-3) */
  refresh(): void {
    if (this.groupStatus() !== 'Open') return;
    forkJoin({
      sessions: this.svc.getSessions$(this.groupId(), this.token, { silent: true }),
      status: this.svc.getStatus$(this.groupId(), this.token, { silent: true }),
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

  // ── Template helpers ─────────────────────────────────────────────────────────
  /** Чи є картка «My Pre-Order» */
  isOwnCard(id: string): boolean { return id === this.ownerSessionId(); }

  /**
   * canRemove: тільки гості (не Owner) і тільки поки статус Open
   * Тест бонусу: після Submit (status=Submitted) → removeButtons.length === 0
   */
  canRemoveParticipant(p: Participant): boolean {
    return p.type === 'Guest' && !this.isSubmitted();
  }
}
