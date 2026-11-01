import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { Roles } from '@app/shared/constants/roles';
import { GroupOrderPageComponent } from '@app/pages/group-order-page/group-order-page.component';

const RESERVATION: ReservationCustomer = {
  id: 'res-1',
  tableId: 'table-1',
  startTime: new Date('2026-10-21T16:00:00Z'),
  endTime: new Date('2026-10-21T17:30:00Z'),
  guestCount: 2,
  status: ReservationStatusCustomer.Reserved,
  restaurantName: 'Green & Tasty',
  locationAddress: 'Rustaveli Avenue 48',
  waiterId: 'waiter-1',
};

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;
  let router: Router;
  let dialog: jasmine.SpyObj<MatDialog>;

  beforeEach(async () => {
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: MatDialog, useValue: dialog },
        provideRouter([]),
      ],
      imports: [CardReservationComponent],
    }).compileComponents();
    router = TestBed.inject(Router);
  });

  it('should create', () => {
    createFixture(RESERVATION);

    expect(component).toBeTruthy();
  });

  it('shows New for five minutes after creation, then expires it', fakeAsync(() => {
    const now = new Date();
    createFixture({
      ...RESERVATION,
      createdDate: now,
    });

    expect(fixture.nativeElement.querySelector('.new-badge')?.textContent.trim()).toBe('New');

    tick(5 * 60 * 1000);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.new-badge')).toBeNull();
  }));

  it('does not show New when the creation timestamp is missing', () => {
    createFixture(RESERVATION);

    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.new-badge')).toBeNull();
  });

  it('uses safe defaults for an unmapped status, table, creator, and feedback data', () => {
    createFixture(RESERVATION);

    expect(component.statusView()).toEqual({ class: '', label: 'Unknown' });
    expect(component.displayedTableLabel()).toBe('Table (Unknown)');
    expect(component.creatorName()).toBe('Unknown');
    expect(component.creatorRole()).toBe(Roles.CUSTOMER);
    expect(component.preOrderItemCount()).toBe(0);
    expect(component.canOpenWaiterPreOrder()).toBeFalse();
    expect(component.isAnonymous()).toBeTrue();
    expect(component.hasFullFeedback()).toBeFalse();

    fixture.componentRef.setInput('statusEnum', {
      [ReservationStatusCustomer.Reserved]: { class: 'reserved', label: 'Reserved' },
    });
    fixture.componentRef.setInput('selectedTableLabel', 'Table 7');
    fixture.componentRef.setInput('createdByName', 'Created by Ada');
    fixture.componentRef.setInput('createdByRole', Roles.WAITER);
    fixture.detectChanges();

    expect(component.statusView()).toEqual({ class: 'reserved', label: 'Reserved' });
    expect(component.displayedTableLabel()).toBe('Table 7');
    expect(component.creatorName()).toBe('Created by Ada');
    expect(component.creatorRole()).toBe(Roles.WAITER);
    expect(component.canOpenWaiterPreOrder()).toBeTrue();
  });

  it('opens group orders and navigates to the pre-order menu', () => {
    createFixture({
      ...RESERVATION,
      tableName: 'Table 7',
      createdByName: 'Ada',
      createdByRole: Roles.WAITER,
    });
    const navigate = spyOn(router, 'navigate');

    component.groupReservation();
    expect(dialog.open).toHaveBeenCalledWith(
      GroupOrderPageComponent,
      jasmine.objectContaining({ data: { reservationId: RESERVATION.id } }),
    );

    component.onPreOrder();
    expect(navigate).toHaveBeenCalledWith(
      ['/menu'],
      jasmine.objectContaining({
        queryParams: jasmine.objectContaining({
          preOrder: 'true',
          reservationId: RESERVATION.id,
          reservationLabel: jasmine.stringMatching(/Rustaveli Avenue 48, Table 7,/),
        }),
      }),
    );
  });

  it('routes waiter pre-orders or opens the order modal based on existing items', () => {
    const preOrder = {
      reservationId: RESERVATION.id,
      orderItems: [{ dish: { id: 'dish-1' }, quantity: 1, state: 0 }],
    };
    createFixture({
      ...RESERVATION,
      tableName: 'Table 7',
      createdByName: 'Ada',
      createdByRole: Roles.WAITER,
      preOrder: preOrder as ReservationCustomer['preOrder'],
    });
    const navigate = spyOn(router, 'navigate');
    const originalStorage = sessionStorage.getItem('waiterPreOrderData');

    try {
      component.onPreOrderWaiter();
      expect(navigate).toHaveBeenCalledWith(
        ['/waiter-pre-order'],
        jasmine.objectContaining({
          state: jasmine.objectContaining({
            reservationId: RESERVATION.id,
            createdByName: 'Ada',
            createdByRole: Roles.WAITER,
            tableName: 'Table 7',
          }),
        }),
      );
      expect(JSON.parse(sessionStorage.getItem('waiterPreOrderData') ?? '{}')).toEqual(
        jasmine.objectContaining({ reservationId: RESERVATION.id }),
      );
    } finally {
      if (originalStorage === null) {
        sessionStorage.removeItem('waiterPreOrderData');
      } else {
        sessionStorage.setItem('waiterPreOrderData', originalStorage);
      }
    }

    fixture.componentRef.setInput('reservation', {
      ...RESERVATION,
      createdByRole: Roles.WAITER,
    });
    fixture.detectChanges();
    component.onPreOrderWaiter();
    expect(component.showWaiterOrderModal()).toBeTrue();

    const order = { reservationId: RESERVATION.id, orderItems: [] };
    const emitted = spyOn(component.preOrder, 'emit');
    component.onWaiterOrderCreated(order);
    expect(component.showWaiterOrderModal()).toBeFalse();
    expect(emitted).toHaveBeenCalledWith(order);

    component.showWaiterOrderModal.set(true);
    component.closeWaiterOrderModal();
    expect(component.showWaiterOrderModal()).toBeFalse();
  });

  it('emits table and status changes only when the table actually changes', () => {
    createFixture(RESERVATION);
    fixture.componentRef.setInput(
      'availableTableOptionsMap',
      new Map([
        ['Current table', 'table-1'],
        ['New table', 'table-2'],
      ]),
    );
    const tableChanged = spyOn(component.tableChanged, 'emit');
    const statusChanged = spyOn(component.statusChanged, 'emit');

    component.onTableChange('Missing table');
    component.onTableChange('Current table');
    expect(tableChanged).not.toHaveBeenCalled();

    component.onTableChange('New table');
    expect(tableChanged).toHaveBeenCalledWith({
      reservationId: RESERVATION.id,
      newTableId: 'table-2',
    });
    component.onStatusChange(ReservationStatusCustomer.Finished);
    expect(statusChanged).toHaveBeenCalledWith({
      id: RESERVATION.id,
      newStatus: ReservationStatusCustomer.Finished,
    });
  });

  it('does not mark malformed or future creation dates as new', () => {
    createFixture({ ...RESERVATION, createdDate: new Date('invalid-date') });
    expect(component.isNew()).toBeFalse();

    fixture.componentRef.setInput('reservation', {
      ...RESERVATION,
      createdDate: new Date(Date.now() + 60_000),
    });
    fixture.detectChanges();
    expect(component.isNew()).toBeFalse();
  });

  function createFixture(reservation: ReservationCustomer): void {
    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('reservation', reservation);
    fixture.detectChanges();
  }
});
