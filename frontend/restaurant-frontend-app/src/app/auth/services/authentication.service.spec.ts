import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthenticationService } from './authentication.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Roles } from '@app/shared/constants/roles';

describe('AuthenticationService', () => {
  let service: AuthenticationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem('auth');
    localStorage.removeItem('user');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(AuthenticationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('sends the selected role in the registration request', async () => {
    const fetchSpy = spyOn(window, 'fetch').and.resolveTo({
      ok: true,
      json: async () => ({}),
    } as Response);

    await service.register('Alice', 'Smith', 'alice@example.com', 'Password123!', 'Password123!', 'Waiter');

    expect(fetchSpy).toHaveBeenCalledWith(
      'https://test.api/register',
      jasmine.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          firstName: 'Alice',
          lastName: 'Smith',
          email: 'alice@example.com',
          password: 'Password123!',
          confirmPassword: 'Password123!',
          role: 'Waiter',
        }),
      })
    );
  });

  it('sends the role with the profile update request', () => {
    service.editProfile('Alice', 'Smith', Roles.WAITER).subscribe();

    const request = httpMock.expectOne('https://test.api/me');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      firstName: 'Alice',
      lastName: 'Smith',
      role: Roles.WAITER,
    });

    request.flush({});
  });

  it('omits the role from the profile update request when not provided', () => {
    service.editProfile('Alice', 'Smith').subscribe();

    const request = httpMock.expectOne('https://test.api/me');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      firstName: 'Alice',
      lastName: 'Smith',
    });
    expect(Object.prototype.hasOwnProperty.call(request.request.body, 'role')).toBeFalse();

    request.flush({});
  });
});
