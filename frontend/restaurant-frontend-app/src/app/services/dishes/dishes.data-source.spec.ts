import { DishesService } from '@app/services/dishes/dishes.service';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishesDataSource } from './dishes.data-source';
import { of, throwError } from 'rxjs';

describe('DishesDataSource', () => {
  let service: jasmine.SpyObj<DishesService>;
  let source: DishesDataSource;

  beforeEach(() => {
    service = jasmine.createSpyObj<DishesService>('DishesService', ['getPopularDishes']);
    source = new DishesDataSource(service);
  });

  it('starts with an empty list and loads normalized first-page dishes', () => {
    const emitted: DishListResponse[][] = [];
    source.connect().subscribe((dishes) => emitted.push(dishes));
    service.getPopularDishes.and.returnValue(
      of({
        items: [
          { id: 'dish-1', name: '', price: 0, imageUrl: '' },
          { id: 'dish-2', name: 'Soup', price: 5, imageUrl: 'soup.png', weight: '250g' },
        ] as DishListResponse[],
        nextPageCursor: 'cursor-2',
      })
    );

    source.loadNextPage();

    expect(service.getPopularDishes).toHaveBeenCalledWith(12, null);
    expect(source.data).toEqual([
      {
        id: 'dish-1',
        name: 'Unknown Dish',
        price: 0,
        imageUrl: 'assets/img/dish.png',
        weight: 'N/A',
      },
      { id: 'dish-2', name: 'Soup', price: 5, imageUrl: 'soup.png', weight: '250g' },
    ] as DishListResponse[]);
    expect(emitted.length).toBe(2);
    expect(source.isLoading.value).toBeFalse();
    expect(source.error.value).toBeNull();
  });

  it('uses the returned cursor to append subsequent pages', () => {
    service.getPopularDishes.and.returnValues(
      of({ items: [{ id: 'first' }] as DishListResponse[], nextPageCursor: 'next' }),
      of({ items: [{ id: 'second' }] as DishListResponse[], nextPageCursor: null })
    );

    source.loadNextPage();
    source.loadNextPage();
    expect(service.getPopularDishes).toHaveBeenCalledWith(12, 'next');
    expect(source.data.map((dish) => dish.id)).toEqual(['first', 'second']);
  });

  it('reports load failures and allows a later retry', () => {
    service.getPopularDishes.and.returnValues(
      throwError(() => new Error('offline')),
      of({ items: [], nextPageCursor: null })
    );

    source.loadNextPage();
    expect(source.error.value).toBe('Could not load dishes. Please try again.');
    expect(source.isLoading.value).toBeFalse();

    source.loadNextPage();
    expect(source.error.value).toBeNull();
    expect(service.getPopularDishes).toHaveBeenCalledTimes(2);
  });

  it('does not request another page after the last page or while loading', () => {
    service.getPopularDishes.and.returnValue(of({ items: [], nextPageCursor: null }));
    source.loadNextPage();
    source.loadNextPage();
    expect(service.getPopularDishes).toHaveBeenCalledTimes(1);

    source.isLoading.next(true);
    source.loadNextPage();
    expect(service.getPopularDishes).toHaveBeenCalledTimes(1);
  });

  it('completes its streams when disconnected', () => {
    let dataCompleted = false;
    let loadingCompleted = false;
    let errorCompleted = false;
    source.connect().subscribe({ complete: () => (dataCompleted = true) });
    source.isLoading.subscribe({ complete: () => (loadingCompleted = true) });
    source.error.subscribe({ complete: () => (errorCompleted = true) });

    source.disconnect();

    expect(dataCompleted).toBeTrue();
    expect(loadingCompleted).toBeTrue();
    expect(errorCompleted).toBeTrue();
  });
});
