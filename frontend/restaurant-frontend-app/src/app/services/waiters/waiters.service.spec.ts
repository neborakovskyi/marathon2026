import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WaitersService } from './waiters.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('WaitersService', () => {
  let service: WaitersService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(WaitersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  let httpMock: HttpTestingController;

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('maps the assigned location response to its id', () => {
    service.getWaiterLocationId('waiter-1').subscribe((locationId) => {
      expect(locationId).toBe('loc-1');
    });
    const request = httpMock.expectOne('https://test.api/waiters/waiter-1/location');
    expect(request.request.method).toBe('GET');
    request.flush({ locationId: 'loc-1' });
  });
});
