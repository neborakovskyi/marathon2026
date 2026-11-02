import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Params } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';

import { DishSort } from '@app/models/enums/dish-sort.model';
import { DishType } from '@app/models/enums/dish-type.model';
import { DishesService } from '@app/services/dishes/dishes.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { SearchStateService } from '@app/services/search-state.service';
import { ToastService } from '@app/services/toast/toast.service';
import { MenuPageComponent } from './menu-page.component';

describe('MenuPageComponent', () => {
  let component: MenuPageComponent;
  let fixture: ComponentFixture<MenuPageComponent>;
  let routeQueryParams: BehaviorSubject<Params>;
  let dishesService: jasmine.SpyObj<DishesService>;
  let router: jasmine.SpyObj<Router>;
  let route: ActivatedRoute;
  let searchState: SearchStateService;

  beforeEach(async () => {
    routeQueryParams = new BehaviorSubject<Params>({});
    route = { queryParams: routeQueryParams.asObservable() } as ActivatedRoute;
    dishesService = jasmine.createSpyObj<DishesService>('DishesService', ['getDishes']);
    dishesService.getDishes.and.returnValue(of([]));
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [MenuPageComponent],
      providers: [
        { provide: ActivatedRoute, useValue: route },
        { provide: Router, useValue: router },
        { provide: DishesService, useValue: dishesService },
        { provide: PreOrdersService, useValue: {} },
        {
          provide: ToastService,
          useValue: jasmine.createSpyObj<ToastService>('ToastService', ['error', 'success']),
        },
      ],
    })
      .overrideComponent(MenuPageComponent, { set: { template: '', providers: [] } })
      .compileComponents();

    fixture = TestBed.createComponent(MenuPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    searchState = TestBed.inject(SearchStateService);
  });

  it('passes the trimmed shared query and route category and sort to the dishes API', fakeAsync(() => {
    routeQueryParams.next({
      category: DishType.MainCourse,
      sort: DishSort.PriceAscending,
    });
    searchState.setQuery(' Pasta ');
    fixture.detectChanges();
    tick(300);

    expect(dishesService.getDishes).toHaveBeenCalledWith(
      DishType.MainCourse,
      DishSort.PriceAscending,
      'Pasta',
    );
  }));

  it('debounces changing the search term and cancels an in-flight request', fakeAsync(() => {
    let requestCancelled = false;
    const pendingRequest = new Observable<never>(() => () => {
      requestCancelled = true;
    });
    dishesService.getDishes.and.callFake((_category, _sort, query) =>
      query === 'Lasagna' ? pendingRequest : of([]),
    );
    dishesService.getDishes.calls.reset();

    searchState.setQuery('Lasagna');
    expect(searchState.query()).toBe('Lasagna');
    fixture.detectChanges();
    tick(299);
    expect(dishesService.getDishes).not.toHaveBeenCalled();
    tick(1);
    expect(dishesService.getDishes).toHaveBeenCalledTimes(1);
    expect(dishesService.getDishes).toHaveBeenCalledWith(
      undefined,
      DishSort.PopularityDescending,
      'Lasagna',
    );
    expect(requestCancelled).toBeFalse();

    searchState.setQuery('Pizza');
    expect(searchState.query()).toBe('Pizza');
    fixture.detectChanges();
    expect(requestCancelled).toBeTrue();
    tick(299);
    expect(dishesService.getDishes).toHaveBeenCalledTimes(1);
    tick(1);

    expect(dishesService.getDishes).toHaveBeenCalledTimes(2);
    expect(dishesService.getDishes).toHaveBeenCalledWith(
      undefined,
      DishSort.PopularityDescending,
      'Pizza',
    );

    searchState.setQuery('Ravioli');
    fixture.detectChanges();
    tick(300);

    expect(dishesService.getDishes).toHaveBeenCalledTimes(3);
    expect(dishesService.getDishes).toHaveBeenCalledWith(
      undefined,
      DishSort.PopularityDescending,
      'Ravioli',
    );
  }));

  it('resets the shared query when the menu is destroyed', () => {
    searchState.setQuery('Pasta');

    fixture.destroy();

    expect(searchState.query()).toBe('');
  });

  it('continues sending the shared search query when category changes', fakeAsync(() => {
    routeQueryParams.next({
      category: DishType.Appetizer,
      sort: DishSort.PriceAscending,
    });
    searchState.setQuery('Pasta');
    fixture.detectChanges();
    tick(300);

    component.onCategorySelected(DishType.MainCourse);

    expect(router.navigate).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({
        queryParams: {
          category: DishType.MainCourse,
          sort: DishSort.PriceAscending,
        },
        queryParamsHandling: 'merge',
      }),
    );

    routeQueryParams.next({
      category: DishType.MainCourse,
      sort: DishSort.PriceAscending,
    });

    expect(searchState.query()).toBe('Pasta');
    expect(dishesService.getDishes.calls.mostRecent().args).toEqual([
      DishType.MainCourse,
      DishSort.PriceAscending,
      'Pasta',
    ]);
  }));

  it('continues sending the shared search query when sort changes', fakeAsync(() => {
    routeQueryParams.next({
      category: DishType.MainCourse,
      sort: DishSort.PriceAscending,
    });
    searchState.setQuery('Pasta');
    fixture.detectChanges();
    tick(300);

    component.onSortChanged({ target: { value: DishSort.PriceDescending } } as unknown as Event);

    expect(router.navigate).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({
        queryParams: {
          category: DishType.MainCourse,
          sort: DishSort.PriceDescending,
        },
        queryParamsHandling: 'merge',
      }),
    );

    routeQueryParams.next({
      category: DishType.MainCourse,
      sort: DishSort.PriceDescending,
    });

    expect(searchState.query()).toBe('Pasta');
    expect(dishesService.getDishes.calls.mostRecent().args).toEqual([
      DishType.MainCourse,
      DishSort.PriceDescending,
      'Pasta',
    ]);
  }));
});
