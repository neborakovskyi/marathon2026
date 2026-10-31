import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthenticationService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });

  it('adds a bearer token when one is available', () => {
    authService.getTokens.and.returnValue({
      idToken: 'id-token',
      accessToken: 'access-token',
    });
    http.get(URL).subscribe();

    const request = httpMock.expectOne(URL);
    expect(request.request.headers.get('Authorization')).toBe('Bearer id-token');
    request.flush({});
  });

  it('does not show an order-not-found toast and still rethrows the error', () => {
    const orderUrl = 'https://test.api/orders/reservation-1';
    let status: number | undefined;
    http.get(orderUrl).subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne(orderUrl).flush(
      { message: 'No order exists' },
      { status: 404, statusText: 'Not Found' }
    );

    expect(status).toBe(404);
    expect(toastService.error).not.toHaveBeenCalled();
  });

  it('maps common authenticated API failures to the expected toast', () => {
    const cases = [
      { status: 401, body: { message: 'ignored' }, title: 'Unauthorized', message: 'Please log in to perform the action!' },
      { status: 403, body: { message: 'Denied' }, title: 'Forbidden', message: 'Denied' },
      { status: 404, body: { message: 'Missing' }, title: 'Not Found', message: 'Missing' },
      { status: 409, body: { message: 'Conflict' }, title: 'Conflict', message: 'Conflict' },
      { status: 422, body: { message: 'Invalid' }, title: 'Error', message: 'Invalid' },
      { status: 500, body: { message: 'ignored' }, title: 'Server Error', message: 'Server error. Please try again later.' },
      { status: 418, body: 'Teapot', title: 'Error', message: 'Teapot' },
    ];

    for (const testCase of cases) {
      http.get(`${URL}?status=${testCase.status}`).subscribe({ error: () => undefined });
      httpMock
        .expectOne(`${URL}?status=${testCase.status}`)
        .flush(testCase.body, { status: testCase.status, statusText: 'Failure' });
      expect(toastService.error).toHaveBeenCalledWith(testCase.message, testCase.title);
    }
  });

  it('uses the guest endpoint error messages without touching the application token', () => {
    authService.getTokens.calls.reset();
    const guestUrl = 'https://test.api/guest/sessions';
    http.post(guestUrl, { guestName: 'Ada' }).subscribe({ error: () => undefined });

    const request = httpMock.expectOne(guestUrl);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush(
      { message: 'Expired token' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(authService.getTokens).not.toHaveBeenCalled();
    expect(toastService.error).toHaveBeenCalledWith(
      'Session expired. Please re-open the invite link.',
      'Unauthorized'
    );
  });

  it('uses guest error details and fallback messages for client and server failures', () => {
    const cases = [
      { status: 400, body: 'Bad payload', title: 'Error', message: 'Bad payload' },
      { status: 403, body: { error: 'Forbidden detail' }, title: 'Forbidden', message: 'Forbidden detail' },
      { status: 404, body: {}, title: 'Not Found', message: 'An unexpected error occurred.' },
      { status: 409, body: { message: 'Already joined' }, title: 'Conflict', message: 'Already joined' },
      { status: 422, body: {}, title: 'Error', message: 'An unexpected error occurred.' },
      { status: 500, body: {}, title: 'Server Error', message: 'Server error. Please try again later.' },
      { status: 418, body: {}, title: 'Error', message: 'An unexpected error occurred.' },
    ];

    for (const testCase of cases) {
      const guestUrl = `https://test.api/guest/sessions/${testCase.status}`;
      http.get(guestUrl).subscribe({ error: () => undefined });
      httpMock
        .expectOne(guestUrl)
        .flush(testCase.body, { status: testCase.status, statusText: 'Failure' });
      expect(toastService.error).toHaveBeenCalledWith(testCase.message, testCase.title);
    }
  });
});
