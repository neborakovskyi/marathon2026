import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import {
  GuestMenuDish,
  GuestMenuResponse,
  GuestSessionsService,
} from '@app/services/guest-sessions/guest-sessions.service';
import { of, throwError } from 'rxjs';
import { GuestPreorderStore } from './guest-preorder.store';

describe('GuestPreorderStore', () => {
  let store: GuestPreorderStore;
  let invites: jasmine.SpyObj<GuestInviteService>;
  let sessions: jasmine.SpyObj<GuestSessionsService>;
  const invite: GuestInvite = {
    GroupReservationId: 'group-1',
    LocationId: 'location-1',
    RestaurantName: 'Tasty Place',
    LocationAddress: '1 Main Street',
    StartTime: '2026-10-17T12:00:00.000Z',
    EndTime: '2026-10-17T13:30:00.000Z',
    MaxGuestCount: 4,
    TableNumber: 8,
    InviterName: 'Ada',
  };
  const session = { SessionId: 'session-1', SessionToken: 'token-1' };
  const dish: GuestMenuDish = {
    id: 'dish-1',
    name: 'Soup',
    description: 'Soup',
    price: 8,
    state: 'Available',
    selectable: true,
  };
  const cartItem = { dishId: 'dish-1', title: 'Soup', price: 8 };

  beforeEach(() => {
    localStorage.clear();
    invites = jasmine.createSpyObj<GuestInviteService>('GuestInviteService', ['getInvite$']);
    sessions = jasmine.createSpyObj<GuestSessionsService>('GuestSessionsService', [
      'createSession',
      'getMenu',
      'addDish',
      'removeDish',
      'submit',
    ]);
    invites.getInvite$.and.returnValue(of(invite));
    sessions.createSession.and.returnValue(of(session));
    sessions.getMenu.and.returnValue(of({ dishes: { Mains: [dish] } }));
    sessions.addDish.and.returnValue(of({}));
    sessions.removeDish.and.returnValue(of({}));
    sessions.submit.and.returnValue(of({}));
    TestBed.configureTestingModule({
      providers: [
        { provide: GuestInviteService, useValue: invites },
        { provide: GuestSessionsService, useValue: sessions },
      ],
    });
    store = TestBed.inject(GuestPreorderStore);
  });

  afterEach(() => localStorage.clear());

  it('initializes a context once and restores saved guest data', () => {
    localStorage.setItem('guestPreorder:ctx-1:guestName', 'Ada');
    localStorage.setItem(
      'guestPreorder:ctx-1:cart',
      JSON.stringify([{ dishId: 'dish-1', title: 'Soup', price: 8, qty: 2 }])
    );
    localStorage.setItem('guestPreorder:ctx-1:guestSessionId', 'saved-session');
    localStorage.setItem('guestPreorder:ctx-1:guestSessionToken', 'saved-token');

    store.initContext('');
    store.initContext('ctx-1');
    store.initContext('ctx-1');

    expect(store.guestName()).toBe('Ada');
    expect(store.cartItems()).toEqual([{ dishId: 'dish-1', title: 'Soup', price: 8, qty: 2 }]);
    expect(store.guestSessionId()).toBe('saved-session');
    expect(store.guestSessionToken()).toBe('saved-token');
    expect(invites.getInvite$).toHaveBeenCalledOnceWith('ctx-1');
    expect(sessions.getMenu).toHaveBeenCalledOnceWith('saved-session', 'saved-token');
  });

  it('ignores malformed saved cart JSON', () => {
    localStorage.setItem('guestPreorder:ctx-2:cart', '{bad json');
    store.initContext('ctx-2');

    expect(store.cartItems()).toEqual([]);
  });

  it('loads invite details and classifies expired versus retryable errors', async () => {
    await store.loadInviteByToken('invite-token');
    expect(store.invite()).toEqual(invite);
    expect(store.inviteLoading()).toBeFalse();
    expect(store.inviteError()).toBeNull();

    invites.getInvite$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 410 }))
    );
    await store.loadInviteByToken('expired');
    expect(store.invite()).toBeNull();
    expect(store.inviteError()).toBe('expired');

    invites.getInvite$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 503 }))
    );
    await store.loadInviteByToken('retry');
    expect(store.inviteError()).toBe('retryable');
    expect(store.inviteLoading()).toBeFalse();
  });

  it('joins a loaded invite with a trimmed name and loads its menu', async () => {
    store.invite.set(invite);
    await store.join('  Ada  ');

    expect(sessions.createSession).toHaveBeenCalledWith({
      groupReservationId: 'group-1',
      guestName: 'Ada',
    });
    expect(store.guestName()).toBe('Ada');
    expect(store.guestSessionId()).toBe('session-1');
    expect(store.guestSessionToken()).toBe('token-1');
    expect(store.menuByCategory()).toEqual({ Mains: [dish] });
    expect(store.joinLoading()).toBeFalse();
  });

  it('reports join preconditions and API errors', async () => {
    await store.join('Guest');
    expect(store.joinError()).toBe('Invite is not loaded yet');

    store.invite.set({ ...invite, GroupReservationId: '' });
    await store.join('Guest');
    expect(store.joinError()).toBe('Invite is missing GroupReservationId');

    store.invite.set(invite);
    sessions.createSession.and.returnValue(throwError(() => new Error('offline')));
    spyOn(console, 'log');
    await store.join('Guest');
    expect(store.joinError()).toBe('Failed to join session');
    expect(store.joinLoading()).toBeFalse();
  });

  it('derives invite display values and menu state', () => {
    expect(store.inviteVm()).toBeNull();
    expect(store.inviteText()).toBeNull();
    store.invite.set(invite);

    expect(store.inviteVm()).toEqual(jasmine.objectContaining({
      inviterName: 'Ada',
      restaurantName: 'Tasty Place',
      locationAddress: '1 Main Street',
      tableNumberText: 'Table 8',
      guestsText: '4 people',
    }));
    expect(store.inviteText()).toContain('1 Main Street, Table 8');
    expect(store.menuCategories()).toEqual([]);
    expect(store.hasMenu()).toBeFalse();
    store.menuByCategory.set({ Empty: [], Mains: [dish] });
    expect(store.menuCategories()).toEqual(['Empty', 'Mains']);
    expect(store.hasMenu()).toBeTrue();

    store.invite.set({
      ...invite,
      InviterName: null as unknown as string,
      RestaurantName: null as unknown as string,
      LocationAddress: null as unknown as string,
      TableNumber: null as unknown as number,
      MaxGuestCount: null as unknown as number,
      StartTime: '',
      EndTime: '',
    });
    expect(store.inviteVm()).toEqual(jasmine.objectContaining({
      inviterName: 'Someone',
      restaurantName: '—',
      locationAddress: '—',
      tableNumberText: '—',
      dateText: '—',
      timeText: '—',
      guestsText: '—',
    }));
  });

  it('loads a menu only for an active session and handles missing menu data', async () => {
    await store.loadMenu();
    expect(sessions.getMenu).not.toHaveBeenCalled();

    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    sessions.getMenu.and.returnValue(of({ dishes: undefined } as unknown as GuestMenuResponse));
    await store.loadMenu();
    expect(store.menuByCategory()).toEqual({});
    expect(store.menuError()).toBeNull();
    expect(store.menuLoading()).toBeFalse();
  });

  it('clears menu state when loading the menu fails', async () => {
    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    store.menuByCategory.set({ Mains: [dish] });
    sessions.getMenu.and.returnValue(throwError(() => new Error('offline')));
    spyOn(console, 'log');

    await store.loadMenu();

    expect(store.menuByCategory()).toEqual({});
    expect(store.menuError()).toBe('Failed to load menu');
    expect(store.menuLoading()).toBeFalse();
  });

  it('updates guest names and cart totals after successful remote changes', async () => {
    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    store.setGuestName(' Ada ');
    expect(store.guestName()).toBe('Ada');

    expect(await store.addDish(cartItem)).toBeTrue();
    expect(store.cartItems()).toEqual([{ ...cartItem, qty: 1 }]);
    expect(await store.addDish(cartItem)).toBeTrue();
    expect(await store.inc('dish-1')).toBeTrue();
    expect(store.cartItems()[0].qty).toBe(3);
    expect(store.total()).toBe(24);
    expect(sessions.addDish).toHaveBeenCalledWith('session-1', 'token-1', 'dish-1', 1);

    expect(await store.dec('dish-1')).toBeTrue();
    expect(store.cartItems()[0].qty).toBe(2);
    expect(await store.remove('dish-1')).toBeTrue();
    expect(store.cartItems()).toEqual([]);
    expect(store.total()).toBe(0);
    expect(sessions.removeDish).toHaveBeenCalledWith('session-1', 'token-1', 'dish-1', 1);
    expect(sessions.removeDish).toHaveBeenCalledWith('session-1', 'token-1', 'dish-1', 2);
  });

  it('rejects cart changes without a session, for unknown dishes, or while busy', async () => {
    expect(await store.addDish(cartItem)).toBeFalse();
    expect(store.cartError()).toBe('Your guest session has expired. Please rejoin the reservation.');
    expect(await store.remove('unknown')).toBeFalse();

    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    store.cartBusy.set(true);
    expect(await store.inc('dish-1')).toBeFalse();
    expect(sessions.addDish).not.toHaveBeenCalled();
  });

  it('leaves local cart unchanged and reports remote quantity errors', async () => {
    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    sessions.addDish.and.returnValue(throwError(() => new Error('offline')));
    spyOn(console, 'log');

    expect(await store.addDish(cartItem)).toBeFalse();
    expect(store.cartItems()).toEqual([]);
    expect(store.cartError()).toBe('Could not update your cart. Please try again.');
    expect(store.cartBusy()).toBeFalse();
  });

  it('requires a non-empty cart and session before submitting a preorder', async () => {
    await store.submitPreorder();
    expect(store.cartError()).toBe('Your guest session has expired. Please rejoin the reservation.');

    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    store.cartError.set(null);
    await store.submitPreorder();
    expect(sessions.submit).not.toHaveBeenCalled();

    store.cartBusy.set(true);
    store.cartItems.set([{ ...cartItem, qty: 1 }]);
    await store.submitPreorder();
    expect(sessions.submit).not.toHaveBeenCalled();
  });

  it('submits the cart, marks success and clears saved guest data', async () => {
    store.initContext('ctx-submit');
    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    store.guestName.set('Ada');
    store.cartItems.set([{ ...cartItem, qty: 1 }]);
    localStorage.setItem('guestPreorder:ctx-submit:guestName', 'Ada');
    localStorage.setItem('guestPreorder:ctx-submit:cart', 'saved');

    await store.submitPreorder();

    expect(sessions.submit).toHaveBeenCalledWith('session-1', 'token-1');
    expect(store.status()).toBe('submitted');
    expect(store.cartBusy()).toBeFalse();
    expect(localStorage.getItem('guestPreorder:ctx-submit:guestName')).toBeNull();
    expect(localStorage.getItem('guestPreorder:ctx-submit:cart')).toBeNull();
  });

  it('reports submit failures and always releases the busy state', async () => {
    store.guestSessionId.set('session-1');
    store.guestSessionToken.set('token-1');
    store.cartItems.set([{ ...cartItem, qty: 1 }]);
    sessions.submit.and.returnValue(throwError(() => new Error('offline')));

    await store.submitPreorder();

    expect(store.cartError()).toBe('Could not submit your pre-order. Please try again.');
    expect(store.status()).toBe('active');
    expect(store.cartBusy()).toBeFalse();
  });

  it('creates a fresh preorder only when context, invite and guest name are ready', async () => {
    await store.createNewPreorder();
    expect(sessions.createSession).not.toHaveBeenCalled();

    store.initContext('ctx-new');
    store.invite.set(invite);
    store.setGuestName(' Ada ');
    store.cartItems.set([{ ...cartItem, qty: 2 }]);
    localStorage.setItem('guestPreorder:ctx-new:cart', 'old');

    await store.createNewPreorder();

    expect(sessions.createSession).toHaveBeenCalledWith({
      groupReservationId: 'group-1',
      guestName: 'Ada',
    });
    expect(store.cartItems()).toEqual([]);
    expect(store.guestSessionId()).toBe('session-1');
    expect(store.status()).toBe('active');
    expect(store.menuByCategory()).toEqual({ Mains: [dish] });
    expect(store.joinLoading()).toBeFalse();
  });

  it('reports failure when creating a replacement preorder', async () => {
    store.initContext('ctx-error');
    store.invite.set(invite);
    store.setGuestName('Ada');
    sessions.createSession.and.returnValue(throwError(() => new Error('offline')));

    await store.createNewPreorder();

    expect(store.menuError()).toBe('Could not start a new pre-order. Please try again.');
    expect(store.menuLoading()).toBeFalse();
    expect(store.joinLoading()).toBeFalse();
  });
});
