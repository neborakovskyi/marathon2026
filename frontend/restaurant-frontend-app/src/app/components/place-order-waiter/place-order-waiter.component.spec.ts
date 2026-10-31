import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { DishesService } from '@app/services/dishes/dishes.service';
import { ToastService } from '@app/services/toast/toast.service';
import { of } from 'rxjs';
import { PlaceOrderWaiterComponent } from './place-order-waiter.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('PlaceOrderWaiterComponent', () => {
  let component: PlaceOrderWaiterComponent;
  let fixture: ComponentFixture<PlaceOrderWaiterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        {
          provide: AuthenticationService,
          useValue: {
            getUser: () => ({
              id: 'waiter-1',
              firstName: 'Jane',
              lastName: 'Smith',
              email: 'jane@example.test',
              role: 'Waiter',
            }),
            getUserNameById$: () => of('John Doe'),
          },
        },
        { provide: DishesService, useValue: jasmine.createSpyObj('DishesService', ['getDishes']) },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
      ],
      imports: [PlaceOrderWaiterComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceOrderWaiterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
