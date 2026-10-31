import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Navigation, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { Order, WaiterPreOrderState } from '@app/models/orders/order.model';
import { OrderItem } from '@app/models/dishes/order-item.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { WaiterPreOrderPageComponent } from './waiter-pre-order-page.component';

describe('WaiterPreOrderPageComponent', () => {
  let component: WaiterPreOrderPageComponent;
  let fixture: ComponentFixture<WaiterPreOrderPageComponent>;
  let orders: jasmine.SpyObj<PreOrdersService>;
  let router: Router;
  const dish = {
    id: 'dish-1',
    name: 'Soup',
    imageUrl: '',
    price: 8,
    state: 'Available',
    weight: '250g',
    dishType: 'MainCourse',
    description: 'Soup',
    calories: '200',
    proteins: '4',
    carbohydrates: '20',
    fats: '5',
    vitamins: '',
  } as DishListResponse;
  const item: OrderItem = { dish, quantity: 2, state: 0 };
  const order: Order = {
    orderId: 'order-1',
    reservationId: 'reservation-1',
    reservationAddress: '1 Main Street',
    reservationTime: '2026-10-17T12:00:00.000Z',
    orderItems: [item],
    status: 0,
  };
  const navigationState: WaiterPreOrderState = {
    reservationId: 'reservation-1',
    createdByName: 'Ada',
    createdByRole: 'Customer',
    tableName: 'Table 7',
  };

  beforeEach(async () => {
    sessionStorage.clear();
    orders = jasmine.createSpyObj<PreOrdersService>(
      'PreOrdersService',
      ['getByReservationId', 'confirmDishInOrder', 'rejectDishInOrder']
    );
    orders.getByReservationId.and.returnValue(of(order));
    orders.confirmDishInOrder.and.returnValue(of({}));
    orders.rejectDishInOrder.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
        { provide: PreOrdersService, useValue: orders },
      ],
      imports: [WaiterPreOrderPageComponent],
    }).compileComponents();
    router = TestBed.inject(Router);
    spyOn(router, 'getCurrentNavigation').and.returnValue({
      extras: { state: navigationState },
    } as unknown as Navigation);
    fixture = TestBed.createComponent(WaiterPreOrderPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => sessionStorage.clear());

  it('loads reservation metadata and order items from navigation state', async () => {
    await fixture.whenStable();
    expect(component.reservationId).toBe('reservation-1');
    expect(component.createdByName).toBe('Ada');
    expect(component.createdByRole).toBe('Customer');
    expect(component.tableName).toBe('Table 7');
    expect(component.reservationTime).toBe(order.reservationTime);
    expect(component.orderItems).toEqual([item]);
    expect(orders.getByReservationId).toHaveBeenCalledWith('reservation-1');
  });

  it('loads state from session storage when navigation state is absent', async () => {
    (router.getCurrentNavigation as jasmine.Spy).and.returnValue(null);
    sessionStorage.setItem('waiterPreOrderData', JSON.stringify(navigationState));

    component.ngOnInit();
    await fixture.whenStable();

    expect(component.reservationId).toBe('reservation-1');
    expect(component.createdByName).toBe('Ada');
    expect(component.orderItems).toEqual([item]);
  });

  it('warns and returns when neither navigation nor stored state has an id', () => {
    (router.getCurrentNavigation as jasmine.Spy).and.returnValue(null);
    sessionStorage.setItem('waiterPreOrderData', JSON.stringify({ tableName: 'Table 1' }));
    const warn = spyOn(console, 'warn');

    component.ngOnInit();

    expect(warn).toHaveBeenCalledWith('Reservation ID not found');
    expect(orders.getByReservationId).toHaveBeenCalledTimes(1);
  });

  it('reports an order loading error', async () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    orders.getByReservationId.and.returnValue(throwError(() => error));

    component.ngOnInit();
    await fixture.whenStable();

    expect(logError).toHaveBeenCalledWith('Error loading order:', error);
  });

  it('removes dishes locally', () => {
    component.orderItems = [item, { ...item, dish: { ...dish, id: 'dish-2' } }];
    component.onRemove('dish-1');
    expect(component.orderItems.map((orderItem) => orderItem.dish.id)).toEqual(['dish-2']);
    component.onRemove('missing');
    expect(component.orderItems).toHaveSize(1);
  });

  it('confirms dishes locally after server confirmation', () => {
    component.reservationId = 'reservation-1';
    component.orderItems = [item];
    component.confirmDish('dish-1');
    expect(orders.confirmDishInOrder).toHaveBeenCalledWith('reservation-1', 'dish-1');
    expect(component.orderItems[0].state).toBe(1);
    expect(component.orderItems[0].dish.status).toBe('Confirmed');

    component.confirmDish('missing');
    expect(component.orderItems[0].state).toBe(1);
  });

  it('reports confirmation failures', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    orders.confirmDishInOrder.and.returnValue(throwError(() => error));
    component.confirmDish('dish-1');
    expect(logError).toHaveBeenCalledWith(error);
  });

  it('rejects dishes locally after server confirmation', () => {
    component.reservationId = 'reservation-1';
    component.orderItems = [item];
    component.rejectDish('dish-1');
    expect(orders.rejectDishInOrder).toHaveBeenCalledWith('reservation-1', 'dish-1');
    expect(component.orderItems[0].state).toBe(2);
    expect(component.orderItems[0].dish.status).toBe('Rejected');

    component.rejectDish('missing');
    expect(component.orderItems[0].state).toBe(2);
  });

  it('reports dish rejection failures', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    orders.rejectDishInOrder.and.returnValue(throwError(() => error));
    component.rejectDish('dish-1');
    expect(logError).toHaveBeenCalledWith('Failed to reject dish:', error);
  });
});
