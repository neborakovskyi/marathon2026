import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { Order } from '@app/models/orders/order.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';

import { CartComponent } from './cart.component';

describe('CartComponent', () => {
  let component: CartComponent;
  let fixture: ComponentFixture<CartComponent>;

  let preOrdersServiceMock: jasmine.SpyObj<PreOrdersService>;
  let routerMock: jasmine.SpyObj<Router>;
  const dish = { id: 'dish-1' } as never;
  const order = (orderId: string, status = 0): Order => ({
    orderId,
    reservationId: `reservation-${orderId}`,
    reservationAddress: '1 Main Street',
    reservationTime: '2026-10-17T12:00:00.000Z',
    status,
    orderItems: [{ dish, quantity: 2, state: 0 }],
  } as Order);

  beforeEach(async () => {
    preOrdersServiceMock =
      jasmine.createSpyObj<PreOrdersService>(
        'PreOrdersService',
        [
          'getPreOrders',
          'cancelPreOrderByReservationId',
          'unsubmitPreOrderByReservationId',
          'submitPreOrderByReservationId',
          'editPreOrderByReservationId',
        ],
      );

    routerMock = jasmine.createSpyObj<Router>(
      'Router',
      ['navigate'],
    );

    preOrdersServiceMock.getPreOrders.and.returnValue(of([]));
    preOrdersServiceMock.cancelPreOrderByReservationId.and.returnValue(of({}));
    preOrdersServiceMock.unsubmitPreOrderByReservationId.and.returnValue(of({}));
    preOrdersServiceMock.submitPreOrderByReservationId.and.returnValue(of({}));
    preOrdersServiceMock.editPreOrderByReservationId.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [CartComponent],
      providers: [
        {
          provide: PreOrdersService,
          useValue: preOrdersServiceMock,
        },
        {
          provide: Router,
          useValue: routerMock,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CartComponent);
    component = fixture.componentInstance;

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit closeCartEvent when closeCart is called', () => {
    spyOn(component.closeCartEvent, 'emit');

    component.closeCart();

    expect(
      component.closeCartEvent.emit,
    ).toHaveBeenCalled();
  });

  it('should return submitted orders with statuses 1 and 3', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 1,
      },
      {
        orderId: 'order-2',
        status: 2,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
      {
        orderId: 'order-4',
        status: 0,
      },
    ] as Order[]);

    expect(
      component
        .submittedOrders()
        .map((order) => order.status),
    ).toEqual([1, 3]);
  });

  it('should return unsubmitted orders with status 0', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 0,
      },
      {
        orderId: 'order-2',
        status: 1,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
    ] as Order[]);

    expect(
      component
        .unsubmittedOrders()
        .map((order) => order.status),
    ).toEqual([0]);
  });

  it('should navigate to reservations when goToReservations is called', () => {
    component.goToReservations();

    expect(routerMock.navigate).toHaveBeenCalledWith([
      '/reservations',
    ]);
  });

  it('loads orders when initialized and clears the list when loading fails', () => {
    const orders = [order('order-1')];
    preOrdersServiceMock.getPreOrders.and.returnValue(of(orders));
    component.ngOnInit();
    expect(component.orders()).toEqual(orders);

    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    preOrdersServiceMock.getPreOrders.and.returnValue(throwError(() => error));
    component.ngOnInit();
    expect(component.orders()).toEqual([]);
    expect(logError).toHaveBeenCalledWith('Error loading orders:', error);
  });

  it('selects submitted and unsubmitted orders for their respective views', () => {
    component.orders.set([order('draft', 0), order('submitted', 1), order('cancelled', 2), order('finished', 3)]);
    expect(component.unsubmittedOrders().map((item) => item.orderId)).toEqual(['draft']);
    expect(component.submittedOrders().map((item) => item.orderId)).toEqual(['submitted', 'finished']);
    expect(component.submittedOrder()).toBeUndefined();
    component.submitOrderId.set('finished');
    expect(component.submittedOrder()?.orderId).toBe('finished');
    component.submitOrderId.set('missing');
    expect(component.submittedOrder()).toBeUndefined();
  });

  it('closes the cart and selects modal state without retaining conflicting dialogs', () => {
    component.submitOrderId.set('order-1');
    component.openCancelModal('order-2');
    expect(component.cancelOrderId()).toBe('order-2');
    expect(component.submitOrderId()).toBeNull();
    component.closeCancelModal();
    expect(component.cancelOrderId()).toBeNull();
    component.submitOrderId.set('order-3');
    component.closeSubmitModal();
    expect(component.submitOrderId()).toBeNull();
    component.closeCart();
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it('cancels a found order and reloads current orders', () => {
    component.orders.set([order('order-1')]);
    component.cancelOrder('order-1');

    expect(preOrdersServiceMock.cancelPreOrderByReservationId).toHaveBeenCalledWith(
      'reservation-order-1'
    );
    expect(component.cancelOrderId()).toBeNull();
    expect(preOrdersServiceMock.getPreOrders).toHaveBeenCalledTimes(2);
  });

  it('ignores unknown cancellations and logs cancellation failures', () => {
    const logError = spyOn(console, 'error');
    component.cancelOrder('missing');
    expect(preOrdersServiceMock.cancelPreOrderByReservationId).not.toHaveBeenCalled();
    expect(logError).toHaveBeenCalledWith('Order with ID missing not found');

    component.orders.set([order('order-1')]);
    const error = new Error('offline');
    preOrdersServiceMock.cancelPreOrderByReservationId.and.returnValue(throwError(() => error));
    component.orders.set([order('order-1')]);
    component.cancelOrder('order-1');
    expect(logError).toHaveBeenCalledWith('Error cancelling order order-1:', error);
  });

  it('unsubmits a selected order and reloads the list', () => {
    component.orders.set([order('order-1', 1)]);
    component.editOrder('order-1');
    expect(preOrdersServiceMock.unsubmitPreOrderByReservationId).toHaveBeenCalledWith(
      'reservation-order-1'
    );
    expect(preOrdersServiceMock.getPreOrders).toHaveBeenCalledTimes(2);

    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    preOrdersServiceMock.unsubmitPreOrderByReservationId.and.returnValue(
      throwError(() => error)
    );
    component.orders.set([order('order-1')]);
    component.editOrder('order-1');
    expect(logError).toHaveBeenCalledWith('Error un-submitting order order-1:', error);
  });

  it('opens the submit modal after submitting an order and handles failures', () => {
    component.orders.set([order('order-1')]);
    component.submitOrder('order-1');
    expect(component.submitOrderId()).toBe('order-1');
    expect(preOrdersServiceMock.submitPreOrderByReservationId).toHaveBeenCalledWith(
      'reservation-order-1'
    );

    component.submitOrderId.set(null);
    preOrdersServiceMock.submitPreOrderByReservationId.and.returnValue(
      throwError(() => new Error('offline'))
    );
    const logError = spyOn(console, 'error');
    component.orders.set([order('order-1')]);
    component.submitOrder('order-1');
    expect(component.submitOrderId()).toBeNull();
    expect(logError).toHaveBeenCalledWith(
      'Error submitting order order-1:',
      jasmine.any(Error)
    );
  });

  it('navigates to the menu when adding a dish to an order', () => {
    component.addDishToOrder('order-1');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/menu']);
  });

  it('updates a dish quantity and reloads only when quantity is positive', () => {
    component.orders.set([order('order-1')]);
    component.updateDishQuantity('order-1', 'dish-1', 4);
    expect(preOrdersServiceMock.editPreOrderByReservationId).toHaveBeenCalledWith(
      'reservation-order-1',
      jasmine.objectContaining({
        orderItems: [{ dish, quantity: 4, state: 0 }],
      })
    );

    const calls = preOrdersServiceMock.editPreOrderByReservationId.calls.count();
    component.updateDishQuantity('order-1', 'dish-1', 0);
    component.updateDishQuantity('missing', 'dish-1', 1);
    expect(preOrdersServiceMock.editPreOrderByReservationId).toHaveBeenCalledTimes(calls);

    const logError = spyOn(console, 'error');
    preOrdersServiceMock.editPreOrderByReservationId.and.returnValue(
      throwError(() => new Error('offline'))
    );
    component.orders.set([order('order-1')]);
    component.updateDishQuantity('order-1', 'dish-1', 3);
    expect(logError).toHaveBeenCalledWith(
      'Error updating item dish-1:',
      jasmine.any(Error)
    );
  });

  it('removes a dish and handles unknown orders and API failures', () => {
    component.orders.set([order('order-1')]);
    component.removeDishFromOrder('order-1', 'dish-1');
    expect(preOrdersServiceMock.editPreOrderByReservationId).toHaveBeenCalledWith(
      'reservation-order-1',
      jasmine.objectContaining({ orderItems: [] })
    );

    const calls = preOrdersServiceMock.editPreOrderByReservationId.calls.count();
    component.removeDishFromOrder('missing', 'dish-1');
    expect(preOrdersServiceMock.editPreOrderByReservationId).toHaveBeenCalledTimes(calls);

    const logError = spyOn(console, 'error');
    preOrdersServiceMock.editPreOrderByReservationId.and.returnValue(
      throwError(() => new Error('offline'))
    );
    component.orders.set([order('order-1')]);
    component.removeDishFromOrder('order-1', 'dish-1');
    expect(logError).toHaveBeenCalledWith(
      'Error removing item dish-1:',
      jasmine.any(Error)
    );
  });

  it('requires an order selection before editing from the submit dialog', () => {
    const logError = spyOn(console, 'error');
    component.editOrderModal();
    expect(logError).toHaveBeenCalledWith('No order selected for editing');

    component.orders.set([order('order-1', 1)]);
    component.submitOrderId.set('order-1');
    component.editOrderModal();
    expect(preOrdersServiceMock.unsubmitPreOrderByReservationId).toHaveBeenCalledWith(
      'reservation-order-1'
    );
    expect(component.submitOrderId()).toBeNull();
  });
});
