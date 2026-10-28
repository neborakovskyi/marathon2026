// ФАЙЛ: src/app/services/group-orders/group-orders.service.ts
// Усі HTTP-дзвінки для Owner Group Order в одному місці.
// Тести level-2 і level-3 перевіряють кожен метод окремо.
 
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
 
import { API_BASE_URL }   from '@app/shared/config/api-base-url.token';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
 
import { GuestInvite }            from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse }  from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse }    from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse }   from '@app/models/group-orders/owner-session.model';
import { SubmittedDish }          from '@app/models/group-orders/submit-group-pre-order.model';
 
/** Параметр для «тихих» фонових запитів: не показувати toast при помилці */
interface SilentOptions { silent?: boolean; }
 
@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
 
  // ── Утіліта: HttpContext з SKIP_ERROR_TOAST ─────────────────────────────────
  private silentContext(): HttpContext {
    return new HttpContext().set(SKIP_ERROR_TOAST, true);
  }
 
  // ── Основні методи ─────────────────────────────────────────────────────────
 
  /**
   * Ланцюжок: GET /reservations/{id}/invite-token → GET /guest/invites/{token}
   * Повертає GuestInvite з GroupReservationId всередині.
   * Тест level-2 перевіряє обидва запити.
   */
  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http
      .get<{ token: string }>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/invite-token`)
      .pipe(
        switchMap(({ token }) =>
          this.http.get<GuestInvite>(`${this.baseUrl}/guest/invites/${encodeURIComponent(token)}`)
        )
      );
  }
 
  /**
   * GET /guest/groups/{groupReservationId}/sessions
   * Список учасників з кошиками.
   * { silent: true } → не показувати toast при фоновій помилці (level-3)
   */
  getSessions$(groupId: string, opts: SilentOptions = {}): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions`,
      opts.silent ? { context: this.silentContext() } : {}
    );
  }
 
  /**
   * GET /reservations/{reservationId}/guest-session-id
   * Повертає рядок (id власної сесії Owner), не об'єкт.
   * Тест: service.getOwnerSessionId$('res-1').subscribe(id => ...)
   */
  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(
        `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`
      )
      .pipe(map(resp => resp.guestSessionId));
  }
 
  /**
   * GET /guest/groups/{groupReservationId}/status
   * { silent: true } → SKIP_ERROR_TOAST (level-3)
   */
  getStatus$(groupId: string, opts: SilentOptions = {}): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/status`,
      opts.silent ? { context: this.silentContext() } : {}
    );
  }
 
  /**
   * POST /group-reservations/{groupReservationId}/submit
   * Тіло: { reservationId }
   */
  submit$(groupId: string, reservationId: string): Observable<SubmittedDish[]> {
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupId)}/submit`,
      { reservationId }
    );
  }
 
  /**
   * DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}
   * Повертає оновлений список учасників (Бонус level-3)
   */
  removeSession$(groupId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions/${encodeURIComponent(guestSessionId)}`
    );
  }
}