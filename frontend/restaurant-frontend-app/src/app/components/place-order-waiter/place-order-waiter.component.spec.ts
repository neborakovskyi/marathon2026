import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { DishesService } from '@app/services/dishes/dishes.service';
import { ToastService } from '@app/services/toast/toast.service';
import { of } from 'rxjs';
import { throwError } from 'rxjs';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { PlaceOrderWaiterComponent } from './place-order-waiter.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('PlaceOrderWaiterComponent', () => {
  let component: PlaceOrderWaiterComponent;
  let fixture: ComponentFixture<PlaceOrderWaiterComponent>;
  let authentication: jasmine.SpyObj<AuthenticationService>;
  let dishes: jasmine.SpyObj<DishesService>;
  let toast: jasmine.SpyObj<ToastService>;
  const waiter = {
    id: 'waiter-1',
    firstName: 'Jane',
    lastName: 'Smith',
    email: 'jane@example.test',
    role: 'Waiter',
  };
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

  beforeEach(async () => {
    authentication = jasmine.createSpyObj<AuthenticationService>(
      'AuthenticationService',
      ['getUser', 'getUserNameById$']
    );
    authentication.getUser.and.returnValue(waiter);
    authentication.getUserNameById$.and.returnValue(of('John Doe'));
    dishes = jasmine.createSpyObj<DishesService>('DishesService', ['getDishes']);
    dishes.getDishes.and.returnValue(of([dish]));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: authentication },
        { provide: DishesService, useValue: dishes },
        { provide: ToastService, useValue: toast },
      ],
      imports: [PlaceOrderWaiterComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceOrderWaiterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.waiterName).toBe('Jane Smith');
    expect(component.customerName).toBe('John Doe');
  });

  it('requires an authenticated waiter before constructing', () => {
    authentication.getUser.and.returnValue(null);
    expect(() => TestBed.createComponent(PlaceOrderWaiterComponent)).toThrowError(
      'User is not authenticated'
    );
    expect(toast.error).toHaveBeenCalledWith(
      'You must be logged in to place an order.',
      'Authentication Error'
    );
  });

  it('shows a fallback customer name when the lookup fails', () => {
    authentication.getUserNameById$.and.returnValue(throwError(() => new Error('offline')));
    const logError = spyOn(console, 'error');
    component.ngOnInit();
    expect(component.customerName).toBe('Unknown Customer');
    expect(toast.error).toHaveBeenCalledWith(
      'Failed to fetch customer name. Please try again later.',
      'Error'
    );
    expect(logError).toHaveBeenCalledWith('Error fetching customer name:', jasmine.any(Error));
  });

  it('searches for dishes only when the query has three non-space characters', () => {
    component.dishNameForm.setValue({ dishName: ' Sou ' });
    component.onDishNameChange();
    expect(dishes.getDishes).toHaveBeenCalledWith(undefined, undefined, ' Sou ');
    expect(component.filteredDishes).toEqual([dish]);

    component.dishNameForm.setValue({ dishName: '  A ' });
    component.onDishNameChange();
    expect(component.filteredDishes).toEqual([]);
    expect(dishes.getDishes).toHaveBeenCalledTimes(1);
  });

  it('clears search results when a dish lookup fails', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    dishes.getDishes.and.returnValue(throwError(() => error));
    component.filteredDishes = [dish];
    component.dishNameForm.setValue({ dishName: 'Soup' });
    component.onDishNameChange();
    expect(component.filteredDishes).toEqual([]);
    expect(logError).toHaveBeenCalledWith('Error fetching dishes:', error);
  });

  it('adds each dish only once, then supports quantity updates and removals', () => {
    component.dishNameForm.setValue({ dishName: 'Soup' });
    component.filteredDishes = [dish];
    component.onDishSelected(dish);
    expect(component.orderItems).toEqual([{ dish, quantity: 1, state: 0 }]);
    component.filteredDishes = [dish];
    component.onDishSelected(dish);
    expect(component.orderItems).toHaveSize(1);
    expect(component.filteredDishes).toEqual([dish]);

    component.onQuantityChanged('dish-1', 3);
    component.onQuantityChanged('missing', 4);
    expect(component.orderItems[0].quantity).toBe(3);
    component.onRemoveDish('dish-1');
    expect(component.orderItems).toEqual([]);
    expect(component.filteredDishes).toEqual([dish]);
  });

  it('validates order creation and emits a valid order with the current reservation', () => {
    fixture.componentRef.setInput('reservationId', 'reservation-2');
    const emit = spyOn(component.placeOrder, 'emit');
    const close = spyOn(component.closeModal, 'emit');
    component.onCreateOrder();
    expect(toast.error).toHaveBeenCalledWith(
      'You must select at least one dish to place an order.',
      'Order Error'
    );
    expect(emit).not.toHaveBeenCalled();

    component.orderItems = [{ dish, quantity: 2, state: 0 }];
    component.onCreateOrder();
    expect(emit).toHaveBeenCalledWith({
      reservationId: 'reservation-2',
      orderItems: component.orderItems,
    });
    expect(close).toHaveBeenCalled();
  });

  it('emits the close event', () => {
    const close = spyOn(component.closeModal, 'emit');
    component.onClose();
    expect(close).toHaveBeenCalled();
  });
});
