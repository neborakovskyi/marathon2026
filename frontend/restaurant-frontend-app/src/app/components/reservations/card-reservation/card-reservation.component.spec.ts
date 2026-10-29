import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

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

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: API_BASE_URL, useValue: 'https://test.api' }, provideRouter([])],
      imports: [CardReservationComponent],
    }).compileComponents();
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

  function createFixture(reservation: ReservationCustomer): void {
    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('r', reservation);
    fixture.detectChanges();
  }
});
