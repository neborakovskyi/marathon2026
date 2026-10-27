import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RegisterComponent } from './register.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Roles } from '@app/shared/constants/roles';
import { provideRouter } from '@angular/router';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
      ],
      imports: [RegisterComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('offers only customer and waiter registration roles', () => {
    const roleOptions = Array.from(
      fixture.nativeElement.querySelectorAll('#role option') as NodeListOf<HTMLOptionElement>
    );

    expect(component.registerForm.get('role')?.value).toBe(Roles.CUSTOMER);
    expect(roleOptions.map((option) => option.value)).toEqual([Roles.CUSTOMER, Roles.WAITER]);
  });
});
