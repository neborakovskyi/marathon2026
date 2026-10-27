import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishesService } from './dishes.service';

describe('DishesService', () => {
  let service: DishesService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(DishesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('normalizes the original array response so consumers always receive items', () => {
    const dish = { id: 'dish-1' } as DishListResponse;
    let result: unknown;

    service.getPopularDishes(12, null).subscribe((page) => result = page);
    const request = httpMock.expectOne((req) => req.url === 'https://test.api/dishes/popular');
    expect(request.request.params.get('Limit')).toBe('12');
    request.flush([dish]);

    expect(result).toEqual({ items: [dish], nextPageCursor: null });
  });
});
