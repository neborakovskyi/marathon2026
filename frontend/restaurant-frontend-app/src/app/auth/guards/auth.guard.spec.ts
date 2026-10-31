import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, UrlTree } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';
import { AuthenticationService } from '../services/authentication.service';
import { AuthGuard } from './auth.guard';

describe('AuthGuard', () => {
  let guard: AuthGuard;
  let router: Router;
  let authentication: jasmine.SpyObj<AuthenticationService>;

  beforeEach(() => {
    authentication = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [], [
      'isAuthorised$',
    ]);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthenticationService, useValue: authentication },
      ],
    });
    guard = TestBed.inject(AuthGuard);
    router = TestBed.inject(Router);
  });

  const routeWithAuthorization = (authorized: boolean): ActivatedRouteSnapshot =>
    ({ data: { authorized } }) as unknown as ActivatedRouteSnapshot;

  it('allows access when authentication matches the route requirement', async () => {
    Object.defineProperty(authentication, 'isAuthorised$', { value: of(true) });
    expect(await firstValueFrom(guard.canActivate(routeWithAuthorization(true)))).toBeTrue();

    Object.defineProperty(authentication, 'isAuthorised$', { value: of(false) });
    expect(await firstValueFrom(guard.canActivate(routeWithAuthorization(false)))).toBeTrue();
  });

  it('redirects unauthenticated users to login for authorized routes', async () => {
    Object.defineProperty(authentication, 'isAuthorised$', { value: of(false) });
    const result = await firstValueFrom(guard.canActivate(routeWithAuthorization(true)));

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('redirects authenticated users away from anonymous-only routes', async () => {
    Object.defineProperty(authentication, 'isAuthorised$', { value: of(true) });
    const result = await firstValueFrom(guard.canActivate(routeWithAuthorization(false)));

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/main');
  });
});
