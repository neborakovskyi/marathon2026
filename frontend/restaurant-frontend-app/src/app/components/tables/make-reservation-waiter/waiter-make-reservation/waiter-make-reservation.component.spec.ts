import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { UserModel } from '@app/auth/models/user.model';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { LocationResponse } from '@app/models/locations/location-response.model';
import { Table } from '@app/models/tables/table.model';
import { TimeRange } from '@app/models/tables/time-range.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { LocationsService } from '@app/services/locations/locations.service';
import { WaitersService } from '@app/services/waiters/waiters.service';
import { TableService } from '@app/services/table/table.service';
import { UsersService } from '@app/services/users/users.service';
import { ToastService } from '@app/services/toast/toast.service';
import { WaiterMakeReservationComponent } from './waiter-make-reservation.component';

describe('WaiterMakeReservationComponent', () => {
  let component: WaiterMakeReservationComponent;
  let fixture: ComponentFixture<WaiterMakeReservationComponent>;
  let authentication: jasmine.SpyObj<AuthenticationService>;
  let locations: jasmine.SpyObj<LocationsService>;
  let waiters: jasmine.SpyObj<WaitersService>;
  let tables: jasmine.SpyObj<TableService>;
  let users: jasmine.SpyObj<UsersService>;
  let toast: jasmine.SpyObj<ToastService>;
  const user = {
    id: 'waiter-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    role: 'Waiter',
  } as UserModel;
  const location: LocationResponse = {
    id: 'loc-1',
    address: '1 Main Street',
  } as unknown as LocationResponse;
  const table: Table = {
    id: 'table-1',
    tableNumber: 8,
    capacity: 4,
    locationId: 'loc-1',
    timeRanges: [],
  };
  const slot: TimeRange = {
    start: '2026-10-17T14:00:00.000Z',
    end: '2026-10-17T15:30:00.000Z',
  };

  beforeEach(async () => {
    authentication = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', ['getUser']);
    authentication.getUser.and.returnValue(user);
    locations = jasmine.createSpyObj<LocationsService>('LocationsService', ['getLocations']);
    locations.getLocations.and.returnValue(of([location]));
    waiters = jasmine.createSpyObj<WaitersService>('WaitersService', ['getWaiterLocationId']);
    waiters.getWaiterLocationId.and.returnValue(of('loc-1'));
    tables = jasmine.createSpyObj<TableService>('TableService', [
      'getAvailableTablesWithoutStartTime$',
      'getTableTimeSlots',
    ]);
    tables.getAvailableTablesWithoutStartTime$.and.returnValue(of([table]));
    tables.getTableTimeSlots.and.returnValue(of([slot]));
    users = jasmine.createSpyObj<UsersService>('UsersService', ['getUsers']);
    users.getUsers.and.returnValue(of([user]));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: authentication },
        { provide: LocationsService, useValue: locations },
        { provide: WaitersService, useValue: waiters },
        { provide: TableService, useValue: tables },
        { provide: UsersService, useValue: users },
        { provide: ToastService, useValue: toast },
      ],
      imports: [WaiterMakeReservationComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(WaiterMakeReservationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the signed-in waiter location, tables and time choices', () => {
    expect(component.user).toBe(user);
    expect(component.locations).toEqual([{ id: 'loc-1', address: '1 Main Street' }]);
    expect(component.locationAddresses).toEqual(['1 Main Street']);
    expect(component.selectedLocationId).toBe('loc-1');
    expect(component.selectedTable).toBe(table);
    expect(component.tableNames).toEqual(['Table 8']);
    expect(component.availableTimeSlots).toEqual([slot]);
    expect(component.startTimeOptions).toEqual([component.formatTime(slot.start)]);
    expect(component.endTimeOptions).toEqual([component.formatTime(slot.end)]);
    expect(component.selectedTimeRange).toBe(slot);
  });

  it('loads locations without a waiter profile and logs location request failures', () => {
    authentication.getUser.and.returnValue(null);
    component.ngOnInit();
    expect(waiters.getWaiterLocationId).toHaveBeenCalledTimes(1);

    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    locations.getLocations.and.returnValue(throwError(() => error));
    component.ngOnInit();
    expect(logError).toHaveBeenCalledWith('Failed to load locations', error);
  });

  it('loads tables for the selected location and retains the first table', () => {
    component.onLocationChange('1 Main Street');
    expect(component.selectedLocation).toBe('1 Main Street');
    expect(component.selectedLocationId).toBe('loc-1');
    expect(tables.getAvailableTablesWithoutStartTime$).toHaveBeenCalledWith({
      location: 'loc-1',
      date: null,
      time: null,
      guests: 1,
    });

    component.selectedLocationId = undefined;
    const callCount = tables.getAvailableTablesWithoutStartTime$.calls.count();
    component.loadTables();
    expect(tables.getAvailableTablesWithoutStartTime$).toHaveBeenCalledTimes(callCount);
  });

  it('ignores unknown locations and tables', () => {
    const previous = component.selectedTable;
    component.onLocationChange('unknown');
    component.onTableChange('Table 999');
    expect(component.selectedLocationId).toBe('loc-1');
    expect(component.selectedTable).toBe(previous);
    expect(tables.getTableTimeSlots).not.toHaveBeenCalledWith('missing', jasmine.anything(), jasmine.anything());
  });

  it('changes tables and loads their available time ranges', () => {
    const secondTable = { ...table, id: 'table-2', tableNumber: 9 };
    component.tables = [table, secondTable];
    component.onTableChange('Table 9');
    expect(component.selectedTable).toBe(secondTable);
    expect(tables.getTableTimeSlots).toHaveBeenCalled();
    expect(component.selectedStartTime).toBe(slot.start);
    expect(component.selectedEndTime).toBe(slot.end);
  });

  it('selects time slots by their displayed start and end times', () => {
    component.availableTimeSlots = [slot];
    const startText = component.formatTime(slot.start);
    const endText = component.formatTime(slot.end);

    component.onStartTimeChange(startText);
    expect(component.selectedStartTime).toBe(slot.start);
    expect(component.selectedEndTime).toBe(slot.end);
    component.onEndTimeChange(endText);
    expect(component.selectedTimeRange).toEqual({ start: slot.start, end: slot.end });

    component.onStartTimeChange('not a slot');
    component.onEndTimeChange('not a slot');
    expect(component.selectedTimeRange).toEqual({ start: slot.start, end: slot.end });
  });

  it('emits a complete visitor reservation and notifies the waiter', () => {
    const emit = spyOn(component.makeReservation, 'emit');
    component.selectedTable = table;
    component.selectedStartTime = slot.start;
    component.selectedEndTime = slot.end;
    component.guests = 3;
    component.reservationType = 'visitor';

    component.onMakeReservation();

    expect(emit).toHaveBeenCalledWith({
      tableId: 'table-1',
      timeRange: { start: slot.start, end: slot.end },
      guests: 3,
      customerEmail: null,
    });
    expect(toast.success).toHaveBeenCalledWith(
      'Reservation created successfully.',
      'Success'
    );
  });

  it('emits the chosen customer email for an existing user and ignores incomplete forms', () => {
    const emit = spyOn(component.makeReservation, 'emit');
    component.selectedTable = table;
    component.selectedStartTime = slot.start;
    component.selectedEndTime = slot.end;
    component.reservationType = 'existingUser';
    component.selectUser(user);
    component.onMakeReservation();
    expect(emit).toHaveBeenCalledWith(jasmine.objectContaining({
      customerEmail: 'ada@example.test',
    }));

    component.selectedStartTime = '';
    component.onMakeReservation();
    expect(emit).toHaveBeenCalledTimes(1);
  });

  it('updates the guest count after debouncing and ignores an undefined count', fakeAsync(() => {
    const initialCalls = tables.getAvailableTablesWithoutStartTime$.calls.count();
    component.onGuestsChange(undefined);
    tick(700);
    expect(tables.getAvailableTablesWithoutStartTime$).toHaveBeenCalledTimes(initialCalls);

    component.onGuestsChange(4);
    tick(700);
    expect(component.guests).toBe(4);
    expect(tables.getAvailableTablesWithoutStartTime$).toHaveBeenCalledTimes(initialCalls + 1);
    component.onGuestsChange(4);
    tick(700);
    expect(tables.getAvailableTablesWithoutStartTime$).toHaveBeenCalledTimes(initialCalls + 1);
  }));

  it('debounces user searches, skips blank searches and tolerates lookup errors', fakeAsync(() => {
    component.searchQuery$.next('  Ada  ');
    tick(700);
    expect(users.getUsers).toHaveBeenCalledWith('  Ada  ');
    expect(component.searchedUsers).toEqual([user]);

    component.searchQuery$.next('   ');
    tick(700);
    expect(users.getUsers).toHaveBeenCalledTimes(1);
    expect(component.searchedUsers).toEqual([]);

    users.getUsers.and.returnValue(throwError(() => new Error('offline')));
    component.searchQuery$.next('Bob');
    tick(700);
    expect(component.searchedUsers).toEqual([]);
  }));

  it('clears selected table and time values when no tables or time slots exist', () => {
    tables.getAvailableTablesWithoutStartTime$.and.returnValue(of([]));
    component.loadTables();
    expect(component.selectedTable).toBeUndefined();
    expect(component.availableTimeSlots).toEqual([]);
    expect(component.selectedStartTime).toBe('');

    component.selectedTimeRange = slot;
    component.selectedStartTime = slot.start;
    component.selectedEndTime = slot.end;
    component.clearSelection();
    expect(component.selectedTimeRange).toEqual({ start: '', end: '' });
    expect(component.selectedEndTime).toBe('');

    tables.getAvailableTablesWithoutStartTime$.and.returnValue(of([table]));
    tables.getTableTimeSlots.and.returnValue(of([]));
    component.loadTables();
    expect(component.availableTimeSlots).toEqual([]);
    expect(component.selectedTimeRange).toEqual({ start: '', end: '' });
  });

  it('handles table and time-slot service errors and closes the modal', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    tables.getAvailableTablesWithoutStartTime$.and.returnValue(throwError(() => error));
    component.loadTables();
    expect(logError).toHaveBeenCalledWith('Failed to load tables:', error);
    tables.getAvailableTablesWithoutStartTime$.and.returnValue(of([table]));
    tables.getTableTimeSlots.and.returnValue(throwError(() => error));
    component.loadTables();
    expect(component.availableTimeSlots).toEqual([]);

    const close = spyOn(component.closeModal, 'emit');
    component.onClose();
    expect(close).toHaveBeenCalled();
  });
});
