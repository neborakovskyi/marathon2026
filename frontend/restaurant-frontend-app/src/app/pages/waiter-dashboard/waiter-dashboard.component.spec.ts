import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import {
  Reservation,
  ReservationCustomer,
  ReservationStatus,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { Table } from '@app/models/tables/table.model';
import { TimeRange } from '@app/models/tables/time-range.model';
import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { LocationResponse } from '@app/models/locations/location-response.model';
import { Order } from '@app/models/orders/order.model';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { TableService } from '@app/services/table/table.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { UsersService } from '@app/services/users/users.service';
import { WaitersService } from '@app/services/waiters/waiters.service';
import { ToastService } from '@app/services/toast/toast.service';
import { LocationsService } from '@app/services/locations/locations.service';
import { WaiterDashboardComponent } from './waiter-dashboard.component';

describe('WaiterDashboardComponent', () => {
  let component: WaiterDashboardComponent;
  let fixture: ComponentFixture<WaiterDashboardComponent>;
  let authentication: jasmine.SpyObj<AuthenticationService>;
  let reservationService: jasmine.SpyObj<ReservationService>;
  let tableService: jasmine.SpyObj<TableService>;
  let ordersService: jasmine.SpyObj<PreOrdersService>;
  let usersService: jasmine.SpyObj<UsersService>;
  let waitersService: jasmine.SpyObj<WaitersService>;
  let locationsService: jasmine.SpyObj<LocationsService>;
  let toast: jasmine.SpyObj<ToastService>;
  const day = new Date(2026, 9, 17);
  const dateText = new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(day);
  const reservation: Reservation = {
    id: 'reservation-1',
    tableId: 'table-1',
    customerId: 'customer-1',
    startTime: new Date(2026, 9, 17, 14, 0),
    endTime: new Date(2026, 9, 17, 15, 30),
    guestCount: 2,
    status: ReservationStatus.Reserved,
    locationAddress: '1 Main Street',
    tableCapacity: 4,
    restaurantName: 'Tasty Place',
    waiterId: 'waiter-1',
  };
  const reservationCustomer = {
    ...reservation,
    status: ReservationStatusCustomer.Reserved,
  } as unknown as ReservationCustomer;
  const timeRange: TimeRange = {
    start: '2026-10-17T14:00:00.000Z',
    end: '2026-10-17T15:30:00.000Z',
  };
  const table: Table = {
    id: 'table-1',
    tableNumber: 7,
    capacity: 4,
    locationId: 'loc-1',
    timeRanges: [],
  };

  beforeEach(async () => {
    authentication = jasmine.createSpyObj<AuthenticationService>(
      'AuthenticationService',
      ['getToken', 'getUser'],
      { user$: of(null) }
    );
    authentication.getToken.and.returnValue('id-token');
    authentication.getUser.and.returnValue({
      id: 'waiter-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      role: 'Waiter',
    } as ReturnType<AuthenticationService['getUser']>);
    reservationService = jasmine.createSpyObj<ReservationService>(
      'ReservationService',
      [
        'getReservationsForUser$',
        'getReservationFeedback',
        'makeReservation$',
        'cancelReservation$',
        'changeReservationStatus$',
        'editReservation$',
      ]
    );
    reservationService.getReservationsForUser$.and.returnValue(of([]));
    reservationService.getReservationFeedback.and.returnValue(of(null));
    reservationService.makeReservation$.and.returnValue(of({ ...reservation } as Reservation));
    reservationService.cancelReservation$.and.returnValue(of({}));
    reservationService.changeReservationStatus$.and.returnValue(
      of({ ...reservation } as unknown as ReservationCustomer)
    );
    reservationService.editReservation$.and.returnValue(of({ ...reservation } as Reservation));
    tableService = jasmine.createSpyObj<TableService>('TableService', [
      'getTableTimeSlots',
      'getAvailableTablesWithoutStartTime$',
      'changeReservationTable$',
    ]);
    tableService.getTableTimeSlots.and.returnValue(of([timeRange]));
    tableService.getAvailableTablesWithoutStartTime$.and.returnValue(of([table]));
    tableService.changeReservationTable$.and.returnValue(
      of({ ...reservationCustomer, tableId: 'table-2' })
    );
    ordersService = jasmine.createSpyObj<PreOrdersService>(
      'PreOrdersService',
      ['getPreOrders', 'getByReservationId', 'createPreOrder']
    );
    ordersService.getPreOrders.and.returnValue(of([]));
    ordersService.getByReservationId.and.returnValue(of({} as Order));
    ordersService.createPreOrder.and.returnValue(of({} as Order));
    usersService = jasmine.createSpyObj<UsersService>('UsersService', ['getUserName']);
    usersService.getUserName.and.returnValue(of('Guest Customer'));
    waitersService = jasmine.createSpyObj<WaitersService>('WaitersService', ['getWaiterLocationId']);
    waitersService.getWaiterLocationId.and.returnValue(of('loc-1'));
    locationsService = jasmine.createSpyObj<LocationsService>(
      'LocationsService',
      ['getLocationById']
    );
    locationsService.getLocationById.and.returnValue(of({
      id: 'loc-1',
      address: '1 Main Street',
    } as unknown as LocationResponse));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error', 'success']);

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
        { provide: AuthenticationService, useValue: authentication },
        { provide: ReservationService, useValue: reservationService },
        { provide: TableService, useValue: tableService },
        { provide: PreOrdersService, useValue: ordersService },
        { provide: UsersService, useValue: usersService },
        { provide: WaitersService, useValue: waitersService },
        { provide: LocationsService, useValue: locationsService },
        { provide: ToastService, useValue: toast },
      ],
      imports: [WaiterDashboardComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(WaiterDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the waiter profile, reservation list, assigned location and tables', () => {
    expect(component.waiterName()).toBe('Ada Lovelace');
    expect(component.role()).toBe('Waiter');
    expect(component.reservationsLoaded()).toBeTrue();
    expect(component.dateOptions().length).toBe(14);
    expect(component.assignedLocationId()).toBe('loc-1');
    expect(component.assignedLocationName()).toBe('1 Main Street');
    expect(component.tables()).toEqual([table]);
    expect(component.tableOptions()).toEqual([
      { text: 'Any Table', value: '' },
      { text: 'Table 7', value: 'table-1' },
    ]);
  });

  it('reports an expired session without requesting reservations', () => {
    authentication.getToken.and.returnValue(undefined);
    component.ngOnInit();
    expect(toast.error).toHaveBeenCalledWith(
      'Your session has expired. Please sign in again.',
      'Authentication'
    );
    expect(component.reservationsLoaded()).toBeTrue();
    expect(reservationService.getReservationsForUser$).toHaveBeenCalledTimes(1);
  });

  it('enriches reservations with feedback, creator and submitted order data', () => {
    const feedback = { service: { rating: 5 }, cuisineExperience: { rating: 4 } };
    const order = { reservationId: 'reservation-1', status: 3 };
    reservationService.getReservationsForUser$.and.returnValue(of([reservation]));
    reservationService.getReservationFeedback.and.returnValue(of(feedback as never));
    ordersService.getByReservationId.and.returnValue(of(order as Order));
    component.ngOnInit();

    const enriched = component.reservations()[0];
    expect(enriched).toEqual(jasmine.objectContaining({
      id: 'reservation-1',
      createdByName: 'Guest Customer',
      createdByRole: 'Customer',
      restaurantName: 'Tasty Place',
      feedback,
      preOrder: order,
      hasSubmittedPreOrder: true,
    }));
    expect(usersService.getUserName).toHaveBeenCalledWith('customer-1');
  });

  it('enriches waiter-created reservations and tolerates missing optional remote data', () => {
    reservationService.getReservationsForUser$.and.returnValue(of([
      { ...reservation, id: 'waiter-reservation', customerId: undefined, restaurantName: undefined },
    ] as unknown as Reservation[]));
    reservationService.getReservationFeedback.and.returnValue(
      throwError(() => new Error('feedback offline'))
    );
    ordersService.getByReservationId.and.returnValue(
      throwError(() => new Error('orders offline'))
    );
    spyOn(console, 'error');
    component.ngOnInit();

    expect(component.reservations()[0]).toEqual(jasmine.objectContaining({
      createdByName: 'Ada Lovelace',
      createdByRole: 'Waiter',
      restaurantName: '',
      feedback: undefined,
      preOrder: undefined,
      hasSubmittedPreOrder: false,
    }));
    expect(usersService.getUserName).not.toHaveBeenCalled();
  });

  it('handles 404 feedback and order responses without noisy errors', () => {
    const error404 = new HttpErrorResponse({ status: 404 });
    reservationService.getReservationsForUser$.and.returnValue(of([reservation]));
    reservationService.getReservationFeedback.and.returnValue(throwError(() => error404));
    ordersService.getByReservationId.and.returnValue(throwError(() => error404));
    const logError = spyOn(console, 'error');
    component.ngOnInit();
    expect(component.reservations()).toHaveSize(1);
    expect(logError).not.toHaveBeenCalled();
  });

  it('filters reservations by date, time and table and builds the card view models', () => {
    const localStart = new Date(2026, 9, 17, 14, 0);
    const matching = { ...reservation, startTime: localStart, tableName: 'Table 7' } as unknown as ReservationCustomer;
    const later = { ...reservation, id: 'later', startTime: new Date(2026, 9, 17, 16, 0), tableId: 'table-2' } as unknown as ReservationCustomer;
    const otherDate = { ...reservation, id: 'other', startTime: new Date(2026, 9, 18, 14, 0) } as unknown as ReservationCustomer;
    component.reservations.set([matching, later, otherDate]);

    expect(component.filteredReservations()).toHaveSize(3);
    component.onDateSelected(dateText);
    expect(component.selectedDate()).toBe(dateText);
    expect(component.timeOptions().length).toBeGreaterThan(0);
    component.onTimeSelected('15:00');
    component.onTableSelected('table-2');
    component.tableOptions.update((options) => [...options, { text: 'Table 9', value: 'table-2' }]);
    expect(component.selectedTableOption()?.text).toBe('Table 9');
    component.applyFilters();

    expect(component.filteredReservations()).toEqual([later]);
    expect(component.filteredReservationsLabel()).toContain('You have 1 reservation');
    expect(component.reservationCards()[0].selectedTableLabel).toBe('Table (Unknown)');
    expect(component.reservationCards()[0].availableTableOptions).toEqual(['Table 7']);

    component.onTableSelected('');
    component.onTimeSelected('');
    component.applyFilters();
    expect(component.filteredReservationsLabel()).toContain('reservations for');
  });

  it('opens and closes the create reservation and feedback QR modals', () => {
    component.onOpenMakeReservationModal();
    expect(component.showMakeReservationModal()).toBeTrue();
    component.onCloseMakeReservationModal();
    expect(component.showMakeReservationModal()).toBeFalse();

    component.assignedLocationId.set('loc-1');
    component.onSendAnonymousFeedback({ ...reservationCustomer, waiterId: '' });
    expect(component.qrReservationId()).toBe('reservation-1');
    expect(component.qrLocationId()).toBe('loc-1');
    expect(component.showQr()).toBeTrue();
    expect(component.isNewFeedback()).toBeTrue();
    component.closeQr();
    expect(component.showQr()).toBeFalse();

    component.onSendAnonymousFeedback({
      ...reservation,
      waiterId: '',
      feedback: { service: { rating: 5 }, cuisineExperience: null },
    } as never);
    expect(component.isNewFeedback()).toBeFalse();
  });

  it('loads the assigned waiter location for feedback QR and handles failures', () => {
    component.onSendAnonymousFeedback(reservationCustomer);
    expect(waitersService.getWaiterLocationId).toHaveBeenCalledWith('waiter-1');
    expect(component.qrLocationId()).toBe('loc-1');
    expect(component.showQr()).toBeTrue();

    waitersService.getWaiterLocationId.and.returnValue(throwError(() => new Error('offline')));
    const logError = spyOn(console, 'error');
    component.onSendAnonymousFeedback(reservationCustomer);
    expect(logError).toHaveBeenCalledWith('Could not load waiter location:', jasmine.any(Error));
    expect(toast.error).toHaveBeenCalledWith(
      'Could not create the feedback QR code.',
      'Feedback'
    );
  });

  it('validates new reservations and adds successful responses', () => {
    component.onMakeReservation({
      tableId: '',
      timeRange,
      guests: 2,
      customerEmail: null,
    });
    expect(toast.error).toHaveBeenCalledWith(
      'Please complete all reservation fields.',
      'Reservation'
    );
    expect(reservationService.makeReservation$).not.toHaveBeenCalled();

    component.onMakeReservation({
      tableId: 'table-1',
      timeRange,
      guests: 2,
      customerEmail: 'guest@example.test',
    });
    expect(component.reservations()).toHaveSize(1);
    expect(component.reservations()[0]).toEqual(jasmine.objectContaining({
      createdByName: 'Ada Lovelace',
      createdByRole: 'Waiter',
      feedback: undefined,
    }));
    expect(component.showMakeReservationModal()).toBeFalse();
    expect(toast.success).toHaveBeenCalledWith(
      'New reservation has been created successfully.',
      'Success'
    );
  });

  it('reports new reservation failures', () => {
    reservationService.makeReservation$.and.returnValue(throwError(() => new Error('offline')));
    component.onMakeReservation({
      tableId: 'table-1',
      timeRange,
      guests: 2,
      customerEmail: null,
    });
    expect(toast.error).toHaveBeenCalledWith(
      'The reservation could not be created.',
      'Reservation'
    );
  });

  it('cancels reservations and handles server and transport errors', () => {
    component.reservations.set([{ ...reservationCustomer }]);
    reservationService.cancelReservation$.and.returnValue(of({ error: 'Conflict' }));
    component.cancelReservation('reservation-1');
    expect(toast.error).toHaveBeenCalledWith('Conflict', 'Reservation');

    reservationService.cancelReservation$.and.returnValue(of({}));
    component.cancelReservation('reservation-1');
    expect(component.reservations()[0].status).toBe(1);
    expect(toast.success).toHaveBeenCalledWith('Reservation has been cancelled.', 'Success');

    reservationService.cancelReservation$.and.returnValue(throwError(() => new Error('offline')));
    component.cancelReservation('reservation-1');
    expect(toast.error).toHaveBeenCalledWith(
      'The reservation could not be cancelled.',
      'Reservation'
    );
  });

  it('updates table and status changes and handles failures', () => {
    component.reservations.set([{ ...reservationCustomer }]);
    component.onReservationTableChanged({ reservationId: 'reservation-1', newTableId: 'table-2' });
    expect(component.reservations()[0].tableId).toBe('table-2');

    tableService.changeReservationTable$.and.returnValue(throwError(() => new Error('offline')));
    component.onReservationTableChanged({ reservationId: 'reservation-1', newTableId: 'table-3' });
    expect(toast.error).toHaveBeenCalledWith('The table could not be updated.', 'Reservation');

    reservationService.changeReservationStatus$.and.returnValue(of({
      ...reservationCustomer,
      status: 2,
    }));
    component.onReservationStatusChanged({ id: 'reservation-1', newStatus: 2 });
    expect(component.reservations()[0].status).toBe(2);
    expect(toast.success).toHaveBeenCalledWith(
      'Reservation status updated successfully.',
      'Success'
    );

    reservationService.changeReservationStatus$.and.returnValue(throwError(() => new Error('offline')));
    component.onReservationStatusChanged({ id: 'reservation-1', newStatus: 3 });
    expect(toast.error).toHaveBeenCalledWith(
      'The reservation status could not be updated.',
      'Reservation'
    );
  });

  it('loads postponement slots and handles missing tables and request errors', () => {
    component.onPostponeReservation({ ...reservationCustomer, tableId: '' });
    expect(toast.error).toHaveBeenCalledWith('This reservation has no table.', 'Reservation');

    component.tables.set([table]);
    component.onPostponeReservation(reservationCustomer);
    expect(component.showPostponeModal()).toBeTrue();
    expect(component.reservationToPostpone()?.id).toBe('reservation-1');
    expect(component.tableForPostpone()).toEqual(jasmine.objectContaining({
      tableNumber: 7,
      capacity: 4,
      timeRanges: [timeRange],
    }));

    component.closePostponeModal();
    expect(component.showPostponeModal()).toBeFalse();
    expect(component.reservationToPostpone()).toBeUndefined();
    expect(component.tableForPostpone()).toBeUndefined();

    tableService.getTableTimeSlots.and.returnValue(throwError(() => new Error('offline')));
    component.onPostponeReservation(reservationCustomer);
    expect(toast.error).toHaveBeenCalledWith(
      'Available time slots could not be loaded.',
      'Reservation'
    );
  });

  it('postpones a selected reservation and handles missing selection and failures', () => {
    component.onPostponeTimeSelected({ table, timeRange });
    expect(reservationService.editReservation$).not.toHaveBeenCalled();

    component.reservations.set([{ ...reservationCustomer }]);
    component.reservationToPostpone.set(reservationCustomer);
    reservationService.editReservation$.and.returnValue(of({
      ...reservation,
      startTime: new Date(2026, 9, 18, 14, 0),
      endTime: new Date(2026, 9, 18, 15, 30),
    } as Reservation));
    component.onPostponeTimeSelected({ table, timeRange });
    expect(component.reservations()[0].startTime).toEqual(new Date(2026, 9, 18, 14, 0));
    expect(component.showPostponeModal()).toBeFalse();

    component.reservationToPostpone.set(reservationCustomer);
    reservationService.editReservation$.and.returnValue(throwError(() => new Error('offline')));
    component.onPostponeTimeSelected({ table, timeRange });
    expect(toast.error).toHaveBeenCalledWith(
      'The reservation could not be postponed.',
      'Reservation'
    );
  });

  it('creates pre-orders for a reservation and handles errors', () => {
    const request: CreateOrderRequest = { reservationId: 'reservation-1', orderItems: [] };
    const order: Order = {
      reservationId: 'reservation-1',
      orderId: 'order-1',
      reservationAddress: '1 Main Street',
      reservationTime: '2026-10-17T14:00:00.000Z',
      orderItems: [],
      status: 0,
    };
    component.reservations.set([{ ...reservationCustomer }]);
    ordersService.createPreOrder.and.returnValue(of(order));
    component.onPreOrderWaiter(request);
    expect(component.reservations()[0].preOrder).toBe(order);
    expect(toast.success).toHaveBeenCalledWith('Pre-order created successfully.', 'Success');

    ordersService.createPreOrder.and.returnValue(throwError(() => new Error('offline')));
    component.onPreOrderWaiter(order);
    expect(toast.error).toHaveBeenCalledWith('The pre-order could not be created.', 'Pre-order');
  });

  it('reports assigned location and reservation loading failures', () => {
    waitersService.getWaiterLocationId.and.returnValue(of(''));
    locationsService.getLocationById.and.returnValue(throwError(() => new Error('offline')));
    component.ngOnInit();
    expect(toast.error).toHaveBeenCalledWith(
      'The waiter location or tables could not be loaded.',
      'Tables'
    );

    reservationService.getReservationsForUser$.and.returnValue(throwError(() => new Error('offline')));
    component.ngOnInit();
    expect(toast.error).toHaveBeenCalledWith('Reservations could not be loaded.', 'Reservations');
  });
});
