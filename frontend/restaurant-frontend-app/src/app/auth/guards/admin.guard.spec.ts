import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Router } from '@angular/router';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

import { AdminGuard } from './admin.guard';

describe('AdminGuard', () => {
  let guard: AdminGuard;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    guard = TestBed.inject(AdminGuard);
    router = TestBed.inject(Router);
    localStorage.clear();
  });

  afterEach(() => localStorage.clear());

  it('should be created', () => {
    expect(guard).toBeTruthy();
  });

  it('denies access without a stored user or role', () => {
    const warn = spyOn(console, 'warn');
    expect(guard.canActivate()).toBeFalse();
    expect(warn).toHaveBeenCalledWith('User not authenticated or missing role');

    localStorage.setItem('user', JSON.stringify({ id: 'user-1' }));
    expect(guard.canActivate()).toBeFalse();
    expect(warn).toHaveBeenCalledTimes(2);
  });

  it('allows admins and redirects other authenticated roles', () => {
    const navigate = spyOn(router, 'navigate');
    localStorage.setItem('user', JSON.stringify({ id: 'admin-1', role: 'Admin' }));
    expect(guard.canActivate()).toBeTrue();
    expect(navigate).not.toHaveBeenCalled();

    localStorage.setItem('user', JSON.stringify({ id: 'customer-1', role: 'Customer' }));
    expect(guard.canActivate()).toBeFalse();
    expect(navigate).toHaveBeenCalledWith(['/reports']);
  });
});
