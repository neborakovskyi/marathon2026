import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestSessionsService } from './guest-sessions.service';

describe('GuestSessionsService', () => {
  let service: GuestSessionsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(GuestSessionsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates a guest session with the group and guest name', () => {
    service.createSession({ groupReservationId: 'group-1', guestName: 'Ada' }).subscribe();
    const request = httpMock.expectOne('https://test.api/guest/sessions');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ groupReservationId: 'group-1', guestName: 'Ada' });
    request.flush({ SessionId: 'session-1', SessionToken: 'token-1' });
  });

  it('loads the guest menu with the session authorization header', () => {
    service.getMenu('session-1', 'guest-token').subscribe();
    const request = httpMock.expectOne('https://test.api/guest/sessions/session-1/menu');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBeTruthy();
    request.flush({ dishes: {} });
  });

  it('adds and removes dishes using the guest session token', () => {
    service.addDish('session-1', 'guest-token', 'dish-1', 2).subscribe();
    const add = httpMock.expectOne('https://test.api/guest/sessions/session-1/dishes');
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ dishId: 'dish-1', quantity: 2 });
    expect(add.request.headers.get('Authorization')).toBeTruthy();
    add.flush({});

    service.removeDish('session-1', 'guest-token', 'dish-1', 1).subscribe();
    const remove = httpMock.expectOne('https://test.api/guest/sessions/session-1/dishes');
    expect(remove.request.method).toBe('DELETE');
    expect(remove.request.body).toEqual({ dishId: 'dish-1', quantity: 1 });
    expect(remove.request.headers.get('Authorization')).toBeTruthy();
    remove.flush({});
  });

  it('submits the guest preorder', () => {
    service.submit('session-1', 'guest-token').subscribe();
    const request = httpMock.expectOne('https://test.api/guest/sessions/session-1/submit');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    expect(request.request.headers.get('Authorization')).toBeTruthy();
    request.flush({});
  });
});
