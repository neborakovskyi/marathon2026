import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { skip } from 'rxjs';
import { Tokens } from '../models/tokens.model';
import { UserModel } from '../models/user.model';
import { AuthenticationService } from './authentication.service';

describe('AuthenticationService', () => {
  let service: AuthenticationService;
  let httpMock: HttpTestingController;
  let fetchSpy: jasmine.Spy<typeof fetch>;
  const tokens: Tokens = { idToken: 'id-token', accessToken: 'access-token' };
  const user = {
    id: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    role: 'Client',
  } as UserModel;
  const response = (body: unknown, ok = true): Response =>
    ({
      ok,
      json: () => Promise.resolve(body),
    }) as Response;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(AuthenticationService);
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(service.jwtHelperService, 'isTokenExpired').and.returnValue(
      false as unknown as Promise<boolean>
    );
    fetchSpy = spyOn(globalThis, 'fetch');
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('initializes user and authorization streams from stored browser data', (done) => {
    TestBed.resetTestingModule();
    localStorage.setItem('user', JSON.stringify(user));
    localStorage.setItem('auth', JSON.stringify(tokens));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    const storedService = TestBed.inject(AuthenticationService);
    const userValues: (UserModel | null)[] = [];
    const authValues: boolean[] = [];
    storedService.user$.subscribe((value) => userValues.push(value));
    storedService.isAuthorised$.subscribe((value) => authValues.push(value));

    expect(storedService.getUser()).toEqual(user);
    expect(userValues).toEqual([user]);
    expect(authValues).toEqual([true]);
    done();
  });

  it('reads the id token and validates the stored token pair', () => {
    expect(service.getToken()).toBeUndefined();
    expect(service.getTokens()).toBeNull();

    localStorage.setItem('auth', JSON.stringify(tokens));
    expect(service.getToken()).toBe('id-token');
    expect(service.getTokens()).toEqual(tokens);
  });

  it('clears credentials when stored tokens are expired or incomplete', () => {
    localStorage.setItem('user', JSON.stringify(user));
    localStorage.setItem('auth', JSON.stringify({ idToken: 'expired', accessToken: 'access' }));
    (service.jwtHelperService.isTokenExpired as jasmine.Spy).and.returnValue(
      true as unknown as Promise<boolean>
    );

    expect(service.getTokens()).toBeNull();
    expect(localStorage.getItem('auth')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();

    localStorage.setItem('auth', JSON.stringify({ idToken: 'id-only' }));
    expect(service.getTokens()).toBeNull();
  });

  it('signs out, clears browser data and emits the logged-out state', (done) => {
    localStorage.setItem('auth', JSON.stringify(tokens));
    localStorage.setItem('user', JSON.stringify(user));
    service.user$.pipe(skip(1)).subscribe((value) => {
      expect(value).toBeNull();
      expect(localStorage.getItem('auth')).toBeNull();
      expect(localStorage.getItem('user')).toBeNull();
      done();
    });

    service.signOut();
  });

  it('posts credentials and loads the user after a successful sign-in', async () => {
    fetchSpy.and.resolveTo(response(tokens));
    (service.jwtHelperService.isTokenExpired as jasmine.Spy).and.returnValue(
      false as unknown as Promise<boolean>
    );
    const authValues: boolean[] = [];
    service.isAuthorised$.subscribe((value) => authValues.push(value));

    const result = await service.signIn('ada@example.test', 'secret');

    expect(fetchSpy).toHaveBeenCalledWith('https://test.api/signin', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'ada@example.test', password: 'secret' }),
    });
    expect(result).toEqual({ success: true });
    expect(JSON.parse(localStorage.getItem('auth') ?? '{}')).toEqual(tokens);
    expect(authValues).toEqual([false, true]);

    const request = httpMock.expectOne('https://test.api/me');
    expect(request.request.method).toBe('GET');
    request.flush(user);
    expect(JSON.parse(localStorage.getItem('user') ?? '{}')).toEqual(user);
  });

  it('returns a server message for a rejected sign-in', async () => {
    const logError = spyOn(console, 'error');
    fetchSpy.and.resolveTo(response({ error: 'Invalid credentials' }, false));

    const result = await service.signIn('ada@example.test', 'wrong');

    expect(result).toEqual({ success: false, message: 'Invalid credentials' });
    expect(logError).toHaveBeenCalledWith('Login failed:', 'Invalid credentials');
    expect(httpMock.expectNone('https://test.api/me')).toBeUndefined();
  });

  it('reports unexpected and malformed sign-in responses', async () => {
    const logError = spyOn(console, 'error');
    fetchSpy.and.resolveTo(response({}));
    await expectAsync(service.signIn('a', 'b')).toBeResolvedTo({
      success: false,
      message: 'Unexpected response from the server. Please try again later.',
    });

    fetchSpy.and.resolveTo({
      ok: true,
      json: () => Promise.reject(new Error('invalid json')),
    } as Response);
    await expectAsync(service.signIn('a', 'b')).toBeResolvedTo({
      success: false,
      message: 'An error occurred during the login process. Please try again later.',
    });
    expect(logError).toHaveBeenCalled();
  });

  it('registers successfully and reports server or unexpected registration errors', async () => {
    fetchSpy.and.resolveTo(response({}));
    await expectAsync(
      service.register('Ada', 'Lovelace', 'ada@example.test', 'secret', 'secret')
    ).toBeResolvedTo({ success: true });
    expect(fetchSpy).toHaveBeenCalledWith('https://test.api/register', jasmine.objectContaining({
      method: 'POST',
      body: JSON.stringify({
        firstName: 'Ada',
        lastName: 'Lovelace',
        email: 'ada@example.test',
        password: 'secret',
        confirmPassword: 'secret',
      }),
    }));

    const logError = spyOn(console, 'error');
    fetchSpy.and.resolveTo(response({ error: 'Email is taken' }, false));
    await expectAsync(service.register('Ada', 'L', 'a', 'p', 'p')).toBeResolvedTo({
      success: false,
      message: 'Email is taken',
    });
    fetchSpy.and.resolveTo(response({}, false));
    await expectAsync(service.register('Ada', 'L', 'a', 'p', 'p')).toBeResolvedTo({
      success: false,
      message: 'Unexpected response from the server. Please try again later.',
    });
    expect(logError).toHaveBeenCalled();
  });

  it('maps a user name response from the API', (done) => {
    service.getUserNameById$('user-1').subscribe((name) => {
      expect(name).toBe('Ada Lovelace');
      done();
    });
    const request = httpMock.expectOne('https://test.api/users/user-1/name');
    request.flush({ name: 'Ada Lovelace' });
  });

  it('updates the profile and refreshes the current user', (done) => {
    localStorage.setItem('auth', JSON.stringify(tokens));
    service.editProfile('Ada', 'Byron').subscribe((message) => {
      expect(message).toBe('Profile updated successfully');
      const reload = httpMock.expectOne('https://test.api/me');
      reload.flush(user);
      expect(JSON.parse(localStorage.getItem('user') ?? '{}')).toEqual(user);
      done();
    });

    const request = httpMock.expectOne('https://test.api/me');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ firstName: 'Ada', lastName: 'Byron' });
    request.flush({});
  });

  it('reports profile update failures to the subscriber', (done) => {
    spyOn(console, 'error');
    service.editProfile('Ada', 'Byron').subscribe({
      error: (error) => {
        expect(error).toBe('Failed to update profile. Please try again later.');
        done();
      },
    });
    httpMock
      .expectOne('https://test.api/me')
      .flush({}, { status: 500, statusText: 'Server Error' });
  });

  it('requires a valid access token to change the password', (done) => {
    let errorValue: unknown;
    service.changePassword('old', 'new').subscribe({ error: (error) => (errorValue = error) });
    expect(errorValue).toBe('User is not authenticated. Please log in again.');
    httpMock.expectNone('https://test.api/me/change-password');
    done();
  });

  it('sends authenticated password changes and handles successful and failed responses', (done) => {
    localStorage.setItem('auth', JSON.stringify(tokens));
    service.changePassword('old', 'new').subscribe((message) => {
      expect(message).toBe('Password changed successfully');
      done();
    });
    const request = httpMock.expectOne('https://test.api/me/change-password');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      oldPassword: 'old',
      newPassword: 'new',
      accessToken: 'access-token',
    });
    request.flush({});
  });

  it('returns a password update error when the API request fails', (done) => {
    localStorage.setItem('auth', JSON.stringify(tokens));
    spyOn(console, 'error');
    service.changePassword('old', 'new').subscribe({
      error: (error) => {
        expect(error).toBe('Failed to change password. Please try again later.');
        done();
      },
    });
    httpMock
      .expectOne('https://test.api/me/change-password')
      .flush({}, { status: 500, statusText: 'Server Error' });
  });

  it('uploads the current user profile image', () => {
    service.uploadMyProfileImage('data:image/png;base64,abc').subscribe();
    const request = httpMock.expectOne('https://test.api/me/profile-image');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ base64Image: 'data:image/png;base64,abc' });
    request.flush('uploaded');
  });
});
