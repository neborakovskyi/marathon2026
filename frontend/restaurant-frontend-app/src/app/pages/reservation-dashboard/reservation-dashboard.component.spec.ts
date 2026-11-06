import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { TableService } from '@app/services/table/table.service';
import { LocationsService } from '@app/services/locations/locations.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { LocationResponse } from '@app/models/locations/location-response.model';
import { FeedbackReservationResponseItem } from '@app/models/feedbacks/feedback-reservation-response-item.model';
import {
  Reservation,
  ReservationCustomer,
  ReservationStatus,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { Table } from '@app/models/tables/table.model';
import { TimeRange } from '@app/models/tables/time-range.model';
import { Roles } from '@app/shared/constants/roles';
import { ReservationDashboardComponent } from './reservation-dashboard.component';

describe('ReservationDashboardComponent', () => {
  let component: ReservationDashboardComponent;
  let fixture: ComponentFixture<ReservationDashboardComponent>;
  let authentication: jasmine.SpyObj<AuthenticationService>;
  let reservationsService: jasmine.SpyObj<ReservationService>;
  let tablesService: jasmine.SpyObj<TableService>;
  let locationsService: jasmine.SpyObj<LocationsService>;
  let ordersService: jasmine.SpyObj<PreOrdersService>;
  let router: Router;
  const reservation: ReservationCustomer = {
    id: 'reservation-1',
    tableId: 'table-1',
    startTime: '2026-10-17T12:00:00.000Z' as unknown as Date,
    endTime: '2026-10-17T13:30:00.000Z' as unknown as Date,
    guestCount: 2,
    status: ReservationStatusCustomer.Reserved,
    restaurantName: 'Tasty Place',
    locationAddress: '1 Main Street',
    waiterId: 'waiter-1',
    tableName: 'Table 7',
    customerId: 'user-1',
  };
  const timeRange: TimeRange = {
    start: '2026-10-17T12:00:00.000Z',
    end: '2026-10-17T13:30:00.000Z',
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
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      role: Roles.CUSTOMER,
    } as ReturnType<AuthenticationService['getUser']>);
    reservationsService = jasmine.createSpyObj<ReservationService>(
      'ReservationService',
      [
        'getReservationsForUser$',
        'getReservationFeedback',
        'cancelReservation$',
        'editReservation$',
      ]
    );
    reservationsService.getReservationsForUser$.and.returnValue(of([]));
    reservationsService.getReservationFeedback.and.returnValue(of(null));
    reservationsService.cancelReservation$.and.returnValue(of({}));
    reservationsService.editReservation$.and.returnValue(of({} as Reservation));
    tablesService = jasmine.createSpyObj<TableService>('TableService', [
      'getTableTimeSlots',
      'getAvailableTablesWithoutStartTime$',
    ]);
    tablesService.getTableTimeSlots.and.returnValue(of([timeRange]));
    tablesService.getAvailableTablesWithoutStartTime$.and.returnValue(of([table]));
    locationsService = jasmine.createSpyObj<LocationsService>('LocationsService', ['getLocations']);
    locationsService.getLocations.and.returnValue(of([
      { id: 'loc-1', address: ' 1 MAIN STREET ' } as unknown as LocationResponse,
    ]));
    ordersService = jasmine.createSpyObj<PreOrdersService>('PreOrdersService', ['getPreOrders']);
    ordersService.getPreOrders.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
        { provide: AuthenticationService, useValue: authentication },
        { provide: ReservationService, useValue: reservationsService },
        { provide: TableService, useValue: tablesService },
        { provide: LocationsService, useValue: locationsService },
        { provide: PreOrdersService, useValue: ordersService },
      ],
      imports: [ReservationDashboardComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(ReservationDashboardComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads customer profile and reservation data on initialization', () => {
    expect(component.firstName()).toBe('Ada');
    expect(component.lastName()).toBe('Lovelace');
    expect(component.role()).toBe(Roles.CUSTOMER);
    expect(component.customerId()).toBe('user-1');
    expect(component.reservationsLoaded()).toBeTrue();
    expect(reservationsService.getReservationsForUser$).toHaveBeenCalled();
  });

  it('redirects users without a session and redirects other roles appropriately', () => {
    const navigate = spyOn(router, 'navigate');
    authentication.getToken.and.returnValue(undefined);
    component.ngOnInit();
    expect(component.reservationsLoaded()).toBeTrue();
    expect(navigate).toHaveBeenCalledWith(['/login']);

    authentication.getToken.and.returnValue('id-token');
    authentication.getUser.and.returnValue({ id: 'waiter', role: Roles.WAITER } as never);
    component.ngOnInit();
    expect(navigate).toHaveBeenCalledWith(['/waiter-reservations']);

    authentication.getUser.and.returnValue({ id: 'admin', role: Roles.ADMIN } as never);
    component.ngOnInit();
    expect(navigate).toHaveBeenCalledWith(['/main']);
  });

  it('toggles the selected reservation status filter', () => {
    component.onStatusSelected(ReservationStatus.Reserved);
    expect(component.selectedReservationStatus()).toBe(ReservationStatus.Reserved);
    component.onStatusSelected(ReservationStatus.Reserved);
    expect(component.selectedReservationStatus()).toBeNull();
  });

  it('maps the selected customer reservation into a reservation view model', () => {
    expect(component.selectedReservationAsReservation()).toBeNull();
    component.customerId.set('current-customer');
    component.selectedReservation.set(reservation);
    component.selectedTable.set(table);

    expect(component.selectedReservationAsReservation()).toEqual(jasmine.objectContaining({
      id: 'reservation-1',
      waiterId: 'waiter-1',
      customerId: 'user-1',
      tableCapacity: 4,
      locationAddress: '1 Main Street',
    }));

    component.selectedTable.set(null);
    component.selectedReservation.set({ ...reservation, customerId: undefined, waiterId: undefined } as never);
    expect(component.selectedReservationAsReservation()?.customerId).toBe('current-customer');
    expect(component.selectedReservationAsReservation()?.waiterId).toBe('');
    expect(component.selectedReservationAsReservation()?.tableCapacity).toBe(2);
  });

  it('cancels reservations and updates selected status only after success', () => {
    component.reservations.set([reservation]);
    component.selectedReservation.set(reservation);
    reservationsService.cancelReservation$.and.returnValue(of({}));

    component.cancelReservation('reservation-1');

    expect(component.reservations()[0].status).toBe(ReservationStatusCustomer.Cancelled);
    expect(component.selectedReservation()?.status).toBe(ReservationStatusCustomer.Cancelled);

    const logError = spyOn(console, 'error');
    reservationsService.cancelReservation$.and.returnValue(of({ error: 'Conflict' }));
    component.cancelReservation('reservation-1');
    expect(logError).toHaveBeenCalledWith('Failed to cancel reservation:', 'Conflict');
    reservationsService.cancelReservation$.and.returnValue(
      throwError(() => new Error('offline'))
    );
    component.cancelReservation('reservation-1');
    expect(logError).toHaveBeenCalledWith('Failed to cancel reservation:', jasmine.any(Error));
  });

  it('loads the table and available time ranges before opening the edit modal', () => {
    component.openEditModal(reservation);

    expect(component.selectedReservation()).toBe(reservation);
    expect(component.selectedTable()).toEqual(jasmine.objectContaining({
      id: 'table-1',
      tableNumber: 7,
      capacity: 4,
      locationId: 'loc-1',
      timeRanges: [timeRange],
    }));
    expect(component.selectedTimeRange()).toEqual(timeRange);
    expect(component.showEditReservationModal()).toBeTrue();

    locationsService.getLocations.and.returnValue(of([]));
    const logError = spyOn(console, 'error');
    component.openEditModal(reservation);
    expect(component.showEditReservationModal()).toBeTrue();
    expect(logError).toHaveBeenCalledWith('Location not found:', reservation.locationAddress);
  });

  it('uses reservation details when the original table is absent', () => {
    tablesService.getAvailableTablesWithoutStartTime$.and.returnValue(of([]));
    component.openEditModal({ ...reservation, tableName: 'Dining table 11' });
    expect(component.selectedTable()).toEqual(jasmine.objectContaining({
      tableNumber: 11,
      capacity: 2,
      locationId: 'loc-1',
    }));
  });

  it('updates an edited reservation and opens its confirmation state', () => {
    component.reservations.set([reservation]);
    component.selectedReservation.set(reservation);
    reservationsService.editReservation$.and.returnValue(of({
      ...reservation,
      startTime: '2026-10-18T12:00:00.000Z',
      endTime: '2026-10-18T13:30:00.000Z',
      status: ReservationStatus.Reserved,
      restaurantName: undefined,
    } as unknown as Reservation));

    component.onSubmitEditReservation(timeRange, 4);

    expect(reservationsService.editReservation$).toHaveBeenCalledWith('reservation-1', timeRange, 4);
    expect(component.reservations()[0].guestCount).toBe(2);
    expect(component.selectedReservation()?.restaurantName).toBe('Tasty Place');
    expect(component.showEditReservationModal()).toBeFalse();
    expect(component.showEditReservationConfirmed()).toBeTrue();
    expect(component.selectedTimeRange()?.start).toBe('2026-10-18T12:00:00.000Z');
  });

  it('does not edit without a selected reservation and handles API errors', () => {
    component.onSubmitEditReservation(timeRange, 2);
    expect(reservationsService.editReservation$).not.toHaveBeenCalled();

    component.selectedReservation.set(reservation);
    reservationsService.editReservation$.and.returnValue(throwError(() => new Error('offline')));
    const logError = spyOn(console, 'error');
    component.onSubmitEditReservation(timeRange, 2);
    expect(logError).toHaveBeenCalledWith('Failed to update reservation:', jasmine.any(Error));
  });

  it('closes, reopens and cancels confirmed reservations', () => {
    component.closeEditReservationModal();
    expect(component.showEditReservationModal()).toBeFalse();
    component.closeReservationConfirmed();
    expect(component.showEditReservationConfirmed()).toBeFalse();
    component.editConfirmedReservation();
    expect(reservationsService.getReservationsForUser$).toHaveBeenCalledTimes(1);

    component.selectedReservation.set(reservation);
    component.showEditReservationConfirmed.set(true);
    component.cancelConfirmedReservation();
    expect(component.showEditReservationConfirmed()).toBeFalse();
    expect(reservationsService.cancelReservation$).toHaveBeenCalledWith('reservation-1');
  });

  it('opens and closes feedback and refreshes the selected reservation', () => {
    component.reservations.set([reservation]);
    component.openFeedback(reservation);
    expect(component.selectedReservation()).toBe(reservation);
    expect(component.showFeedbackModal()).toBeTrue();
    component.closeFeedback();
    expect(component.showFeedbackModal()).toBeFalse();

    const feedback = {
      service: { rating: 5 },
      cuisineExperience: { rating: 4 },
    } as unknown as FeedbackReservationResponseItem;
    reservationsService.getReservationFeedback.and.returnValue(of(feedback));
    component.onFeedbackUpdated({ reservationId: 'reservation-1' } as never);
    expect(component.reservations()[0].feedback).toEqual(feedback);
    expect(component.selectedReservation()?.feedback).toEqual(feedback);
  });

  it('handles feedback loading errors and missing feedback', () => {
    component.reservations.set([reservation]);
    component.selectedReservation.set(reservation);
    reservationsService.getReservationFeedback.and.returnValue(of(null));
    component.onFeedbackUpdated({ reservationId: 'reservation-1' } as never);
    expect(component.reservations()[0].feedback).toBeUndefined();
    expect(component.selectedReservation()?.feedback).toBeUndefined();

    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    reservationsService.getReservationFeedback.and.returnValue(throwError(() => error));
    component.onFeedbackUpdated({ reservationId: 'reservation-1' } as never);
    expect(logError).toHaveBeenCalledWith('Failed to refresh reservation feedback:', error);
  });

  it('shows safe defaults for missing edit table values', () => {
    tablesService.getAvailableTablesWithoutStartTime$.and.returnValue(of([]));
    locationsService.getLocations.and.returnValue(of([
      { id: 'loc-1', address: reservation.locationAddress } as unknown as LocationResponse,
    ]));
    component.openEditModal({ ...reservation, tableName: 'No digits' });
    expect(component.selectedTable()?.tableNumber).toBe(0);
    expect(component.selectedTable()?.capacity).toBe(2);
  });
});
