// ФАЙЛ: src/app/services/group-orders/group-orders.service.ts
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpContext, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';

import { API_BASE_URL }    from '@app/shared/config/api-base-url.token';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';

import { GuestInvite }           from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse }   from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse }  from '@app/models/group-orders/owner-session.model';
import { SubmittedDish }         from '@app/models/group-orders/submit-group-pre-order.model';

interface SilentOptions { silent?: boolean; }

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  // ── Тихий контекст — SKIP_ERROR_TOAST має default=false (з токена).
  //    При silent=true явно ставимо true.
  private silentCtx(): HttpContext {
    return new HttpContext().set(SKIP_ERROR_TOAST, true);
  }

  /**
   * Ланцюжок: GET /reservations/{id}/invite-token
   *   → GET /guest/invites/{token}
   */
  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http
      .get<{ token: string }>(`${this.baseUrl}/reservations/${reservationId}/invite-token`)
      .pipe(
        switchMap(({ token }) =>
          this.http.get<GuestInvite>(`${this.baseUrl}/guest/invites/${token}`)
        )
      );
  }

  /**
   * GET /guest/groups/{groupReservationId}/sessions
   */
  getSessions$(groupId: string, sessionToken: string| undefined, opts: SilentOptions = {}): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${groupId}/sessions`,
      opts.silent ? { context: this.silentCtx(), headers: this.authHeaders(sessionToken)} : {headers: this.authHeaders(sessionToken)}
    );
  }

  /**
   * GET /reservations/{reservationId}/guest-session-id
   */
  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${reservationId}/guest-session-id`)
      .pipe(map(r => r.guestSessionId));
  }

  /** GET /guest/groups/{groupReservationId}/status */
  getStatus$(groupId: string, sessionToken: string| undefined, opts: SilentOptions = {}): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${groupId}/status`,
      opts.silent ? { context: this.silentCtx(), headers: this.authHeaders(sessionToken)} : {headers: this.authHeaders(sessionToken)}
    );
  }

  /**
   * POST /group-reservations/{groupReservationId}/submit
   */
  submit$(groupId: string, reservationId: string): Observable<SubmittedDish[]> {
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${groupId}/submit`,
      { reservationId }
    );
  }

  /**
   * DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}
   */
  removeSession$(groupId: string, guestSessionId: string, sessionToken: string| undefined): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${groupId}/sessions/${guestSessionId}`,
            { headers: this.authHeaders(sessionToken) }
    );
  }

  private authHeaders(token: string| undefined) {
        return new HttpHeaders({
          Authorization: `Bearer ${token}`,
          // "Access-Control-Allow-Origin": "*",
          // "Access-Control-Allow-Headers": "Content-Type, Authorization",
          // "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, OPTIONS",
        });
    }
}
