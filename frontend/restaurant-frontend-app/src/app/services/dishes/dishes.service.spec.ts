import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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

  it('normalizes an array response from the popular-dishes endpoint', () => {
    const dishes = [{ id: 'dish-1' }] as DishListResponse[];
    let result: unknown;
    service.getPopularDishes(12, null).subscribe((page) => (result = page));

    const request = httpMock.expectOne((req) => req.url === 'https://test.api/dishes/popular');
    expect(request.request.params.keys()).toEqual([]);
    request.flush(dishes);

    expect(result).toEqual({ items: dishes, nextPageCursor: null });
  });

  it('keeps a page response and its cursor', () => {
    service.getPopularDishes(12, 'next-page').subscribe((page) => {
      expect(page).toEqual({ items: [], nextPageCursor: 'next-page-2' });
    });

    const request = httpMock.expectOne(
      (req) => req.url === 'https://test.api/dishes/popular' && req.params.has('ExclusiveStartKeyJson')
    );
    expect(request.request.params.get('ExclusiveStartKeyJson')).toBe('next-page');
    request.flush({ items: [], nextPageCursor: 'next-page-2' });
  });

  it('normalizes missing page items and cursor', () => {
    service.getPopularDishes(12, null).subscribe((page) => {
      expect(page).toEqual({ items: [], nextPageCursor: null });
    });
    httpMock
      .expectOne((req) => req.url === 'https://test.api/dishes/popular')
      .flush({});
  });

  it('sends only provided dish filters', () => {
    service.getDishes('MainCourse', 'PriceDescending', 'Pasta').subscribe();
    const request = httpMock.expectOne('https://test.api/dishes?dishType=MainCourse&sortBy=PriceDescending&name=Pasta');
    expect(request.request.method).toBe('GET');
    request.flush([]);

    service.getDishes().subscribe();
    const unfiltered = httpMock.expectOne('https://test.api/dishes');
    expect(unfiltered.request.params.keys()).toEqual([]);
    unfiltered.flush([]);
  });
});
