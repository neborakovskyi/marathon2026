import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { Order } from '@app/models/orders/order.model';
import { PreOrdersService } from './pre-orders.service';

describe('PreOrdersService', () => {
  let service: PreOrdersService;
  let httpMock: HttpTestingController;
  const dish = { id: 'dish-1' } as DishListResponse;
  const order = (items: Order['orderItems']): Order =>
    ({ reservationId: 'reservation-1', orderItems: items }) as Order;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(PreOrdersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('gets orders and a single reservation order', () => {
    service.getPreOrders().subscribe();
    httpMock.expectOne('https://test.api/orders').flush([]);

    service.getByReservationId('reservation-1').subscribe();
    const request = httpMock.expectOne('https://test.api/orders/reservation-1');
    expect(request.request.method).toBe('GET');
    request.flush(order([]));
  });

  it('creates an order when no order exists for the reservation', () => {
    service.addDishToPreOrderByReservationId('reservation-1', dish, 2).subscribe();
    httpMock.expectOne('https://test.api/orders').flush([]);

    const request = httpMock.expectOne('https://test.api/orders');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      reservationId: 'reservation-1',
      orderItems: [{ dishId: 'dish-1', quantity: 2 }],
    });
    request.flush({});
  });

  it('increments an existing dish and updates the order', () => {
    const existing = { dish, quantity: 1, state: 0 };
    service.addDishToPreOrderByReservationId('reservation-1', dish, 2).subscribe();
    httpMock
      .expectOne('https://test.api/orders')
      .flush([order([existing])]);

    const request = httpMock.expectOne('https://test.api/orders/reservation-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ orderItems: [{ dishId: 'dish-1', quantity: 3 }] });
    request.flush({});
  });

  it('adds a new dish to an existing order', () => {
    const otherDish = { id: 'dish-2' } as DishListResponse;
    service.addDishToPreOrderByReservationId('reservation-1', otherDish, 1).subscribe();
    httpMock
      .expectOne('https://test.api/orders')
      .flush([order([{ dish, quantity: 1, state: 0 }])]);

    const request = httpMock.expectOne('https://test.api/orders/reservation-1');
    expect(request.request.body).toEqual({
      orderItems: [
        { dishId: 'dish-1', quantity: 1 },
        { dishId: 'dish-2', quantity: 1 },
      ],
    });
    request.flush({});
  });

  it('maps created order items to dish ids', () => {
    service
      .createPreOrder({
        reservationId: 'reservation-1',
        orderItems: [{ dish, quantity: 3, state: 0 }],
      } as Order)
      .subscribe();

    const request = httpMock.expectOne('https://test.api/orders');
    expect(request.request.body).toEqual({
      reservationId: 'reservation-1',
      orderItems: [{ dishId: 'dish-1', quantity: 3 }],
    });
    request.flush({});
  });

  it('deletes an empty order rather than updating it', () => {
    service.editPreOrderByReservationId('reservation-1', order([])).subscribe();
    const request = httpMock.expectOne('https://test.api/orders/reservation-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({});
  });

  it('updates, cancels, submits, and un submits reservation orders', () => {
    service.editPreOrderByReservationId(
      'reservation-1',
      order([{ dish, quantity: 2, state: 0 }])
    ).subscribe();
    const update = httpMock.expectOne('https://test.api/orders/reservation-1');
    expect(update.request.method).toBe('PUT');
    update.flush({});

    service.cancelPreOrderByReservationId('reservation-1').subscribe();
    const cancel = httpMock.expectOne('https://test.api/orders/reservation-1');
    expect(cancel.request.method).toBe('DELETE');
    cancel.flush({});

    service.submitPreOrderByReservationId('reservation-1').subscribe();
    const submit = httpMock.expectOne('https://test.api/orders/reservation-1/submit');
    expect(submit.request.method).toBe('POST');
    expect(submit.request.body).toEqual({});
    submit.flush({});

    service.unsubmitPreOrderByReservationId('reservation-1').subscribe();
    const unsubmit = httpMock.expectOne('https://test.api/orders/reservation-1/unsubmit');
    expect(unsubmit.request.method).toBe('POST');
    unsubmit.flush({});
  });

  it('confirms and rejects individual dishes', () => {
    service.confirmDishInOrder('reservation-1', 'dish-1').subscribe();
    const confirm = httpMock.expectOne(
      'https://test.api/reservations/reservation-1/order/confirmations/dish-1'
    );
    expect(confirm.request.method).toBe('PATCH');
    confirm.flush({});

    service.rejectDishInOrder('reservation-1', 'dish-1').subscribe();
    const reject = httpMock.expectOne(
      'https://test.api/reservations/reservation-1/order/cancellations/dish-1'
    );
    expect(reject.request.method).toBe('PATCH');
    reject.flush({});
  });
});
