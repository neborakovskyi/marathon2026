import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';

import { ReservationConfirmedComponent } from './reservation-confirmed.component';

describe('ReservationConfirmedComponent', () => {
  const reservation: Reservation = {
    id: 'reservation-1',
    tableId: 'table-1',
    customerId: 'customer-1',
    startTime: new Date('2026-10-31T12:00:00Z'),
    endTime: new Date('2026-10-31T13:30:00Z'),
    guestCount: 2,
    status: ReservationStatus.Reserved,
    locationAddress: '1 Test Street',
    tableCapacity: 4,
    restaurantName: 'Test Restaurant',
    waiterId: 'waiter-1',
  };

  let component: ReservationConfirmedComponent;
  let fixture: ComponentFixture<ReservationConfirmedComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReservationConfirmedComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ReservationConfirmedComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('reservation', reservation);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
