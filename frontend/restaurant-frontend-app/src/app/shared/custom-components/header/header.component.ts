import {
  Component,
  OnDestroy,
  OnInit,
  ChangeDetectionStrategy,
  signal,
  computed,
  effect,
  inject,
} from '@angular/core';
import { Roles } from '@app/shared/constants/roles';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { UserModel } from '@app/auth/models/user.model';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { AsyncPipe, CommonModule } from '@angular/common';
import { interval, Subscription } from 'rxjs';
import { NotificationsService } from '@app/services/notifications/notifications.service';
import { Notification } from '@app/models/notifications/notification.model';
import { CartComponent } from '@app/components/cart/cart.component';
import { GuestPreorderStore } from '@app/pages/guest-pre-order-page/state/guest-preorder.store';
// ── NEW ──────────────────────────────────────────────────────────────────────
import { HeaderModeService } from '@app/services/header-mode.service';
// ─────────────────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive, AsyncPipe, CartComponent, CommonModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: true,
})
export class HeaderComponent implements OnInit, OnDestroy {
  private readonly POLLING_INTERVAL = 60000;
  private isDestroyed = false;

  protected readonly Roles = Roles;

  user = signal<UserModel | null>(null);
  notifications = signal<Notification[]>([]);
  unreadCount = computed(() => this.notifications().filter((n) => !n.isRead).length);

  showDropdown = signal(false);
  showNotificationsDropdown = signal(false);
  isMobileMenuOpen = signal(false);
  showCart = signal(false);

  // ── NEW: поиск ──────────────────────────────────────────────────────────
  searchQuery = signal('');
  // ─────────────────────────────────────────────────────────────────────────

  private pollingSubscription?: Subscription;
  private userSubscription?: Subscription;

  protected readonly authService = inject(AuthenticationService);
  private readonly notificationsService = inject(NotificationsService);
  private readonly router = inject(Router);
  private readonly guestPreorderStore = inject(GuestPreorderStore);
  // ── NEW ──────────────────────────────────────────────────────────────────
  protected readonly headerMode = inject(HeaderModeService);
  // ─────────────────────────────────────────────────────────────────────────

  get isGuestPreorderRoute(): boolean {
    return this.router.url.startsWith('/join/');
  }

  get isSubmittedGuestPreorder(): boolean {
    return this.isGuestPreorderRoute && this.guestPreorderStore.status() === 'submitted';
  }

  constructor() {
    effect(() => {
      const u = this.user();
      if (u?.role === Roles.WAITER) {
        this.startNotificationsPolling();
      } else {
        this.stopNotificationsPolling();
        this.notifications.set([]);
      }
    });
  }

  ngOnInit() {
    this.userSubscription = this.authService.user$.subscribe((u) => this.user.set(u));
  }

  ngOnDestroy() {
    this.isDestroyed = true;
    this.stopNotificationsPolling();
    this.userSubscription?.unsubscribe();
  }

  startNotificationsPolling() {
    if (this.isDestroyed || this.pollingSubscription) return;
    setTimeout(() => this.loadNotifications(), 100);
    this.pollingSubscription = interval(this.POLLING_INTERVAL).subscribe({
      next: () => this.loadNotifications(),
      error: (err) => { console.error('Polling error', err); this.stopNotificationsPolling(); },
    });
  }

  stopNotificationsPolling() {
    this.pollingSubscription?.unsubscribe();
    this.pollingSubscription = undefined;
  }

  loadNotifications() {
    this.notificationsService.loadNotifications().subscribe({
      next: (notifications) => this.notifications.set(notifications),
      error: (error) => console.error('Failed to load notifications', error),
    });
  }

  openDropdown(): void { this.showDropdown.set(true); }
  closeDropdown(): void { this.showDropdown.set(false); }

  onDropdownFocusOut(event: FocusEvent): void {
    const wrapper = event.currentTarget as HTMLElement;
    const next = event.relatedTarget as Node | null;
    if (next && wrapper.contains(next)) return;
    this.closeDropdown();
  }

  toggleMobileMenu() { this.isMobileMenuOpen.update((v) => !v); }
  closeMobileMenu() { this.isMobileMenuOpen.set(false); }

  logout(event?: Event): void {
    event?.stopPropagation();
    this.authService.signOut();
    this.closeDropdown();
    this.closeMobileMenu();
    this.closeNotificationsDropdown();
    this.showCart.set(false);
    void this.router.navigate(['/main']);
  }

  toggleNotificationsDropdown() { this.showNotificationsDropdown.update((v) => !v); }
  closeNotificationsDropdown() { this.showNotificationsDropdown.set(false); }

  markNotificationAsRead(notificationId: string) {
    this.notificationsService.markAsRead(notificationId).subscribe({
      next: () => {
        this.notifications.update((list) =>
          list.map((n) => (n.id === notificationId ? { ...n, isRead: true } : n)),
        );
      },
      error: (err) => console.error('Failed to mark notification as read', err),
    });
  }

  toggleCart() { this.showCart.update((v) => !v); }
}
