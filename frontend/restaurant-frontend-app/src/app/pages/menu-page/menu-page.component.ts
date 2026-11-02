import { Component, OnDestroy, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { DishCardComponent } from '@app/shared/custom-components/dish-card/dish-card.component';
import { DishesService } from '@app/services/dishes/dishes.service';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { MainCoverImageComponent } from '@app/shared/custom-components/main-cover-image/main-cover-image.component';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { DishSort } from '@app/models/enums/dish-sort.model';
import { DishType } from '@app/models/enums/dish-type.model';
import { DishSortingLabels, DishTypeLabels } from '@app/shared/mappers/dish-enum.mapper';
import { DishModalComponent } from '@app/components/dish-modal/dish-modal.component';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { SearchStateService } from '@app/services/search-state.service';
import {
  combineLatest,
  catchError,
  distinctUntilChanged,
  map,
  of,
  Subscription,
  switchMap,
  tap,
  timer,
} from 'rxjs';

@Component({
  selector: 'app-menu-page',
  standalone: true,
  imports: [
    RouterModule,
    DishCardComponent,
    MainCoverImageComponent,
    DishModalComponent
  ],
  templateUrl: './menu-page.component.html',
  styleUrl: './menu-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  providers: [DishesService],
})
export class MenuPageComponent implements OnDestroy, OnInit {
  private readonly SEARCH_DEBOUNCE_MS = 300;
  private toast = inject(ToastService);
  private menuStateSubscription?: Subscription;
  public dishes: DishListResponse[] = [];
  public filteredDishes: DishListResponse[] = [];
  public loadingDishes = true;

  public isPreOrderEnabled = false;
  public preOrderReservationId?: string;
  public preOrderReservationLabel?: string;

  public sortOptions = Object.values(DishSort);
  public sortLabels = DishSortingLabels;

  public menuCategories = Object.values(DishType);
  public menuCategoryLabels = DishTypeLabels;

  public selectedCategory: DishType | null = null; // initially show all
  public selectedSort: DishSort = DishSort.PopularityDescending;

  private readonly dishesService = inject(DishesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly orderService = inject(PreOrdersService);
  private readonly searchState = inject(SearchStateService);
  private readonly searchQuery$ = toObservable(this.searchState.query);

  ngOnInit(): void {
    const routeState$ = this.route.queryParams
      .pipe(
        map((params) => {
          const preOrder = params['preOrder'];
          this.isPreOrderEnabled = !!preOrder;
          if (this.isPreOrderEnabled) {
            this.preOrderReservationId = params['reservationId'];
            this.preOrderReservationLabel = params['reservationLabel'];
            if (!this.preOrderReservationId) {
              console.error('Pre-order is enabled but no reservation ID provided.');
              this.toast.error('Pre-order is enabled but no reservation ID provided.', 'Error');
              this.isPreOrderEnabled = false;
            }
          } else {
            this.preOrderReservationId = undefined;
            this.preOrderReservationLabel = undefined;
          }

          const categoryValue = params['category'];
          const sortValue = params['sort'];

          this.selectedCategory =
            typeof categoryValue === 'string' &&
            Object.values(DishType).includes(categoryValue as DishType)
              ? (categoryValue as DishType)
              : null;
          this.selectedSort =
            typeof sortValue === 'string' && Object.values(DishSort).includes(sortValue as DishSort)
              ? (sortValue as DishSort)
              : DishSort.PopularityDescending;

          return {
            category: this.selectedCategory ?? undefined,
            sort: this.selectedSort,
          };
        }),
        distinctUntilChanged(
          (previous, current) =>
            previous.category === current.category && previous.sort === current.sort,
        ),
      );
    const searchQuery$ = this.searchQuery$.pipe(
      map((query) => query.trim()),
      distinctUntilChanged(),
    );
    let appliedSearchTerm = this.searchState.query().trim();

    this.menuStateSubscription = combineLatest([routeState$, searchQuery$])
      .pipe(
        distinctUntilChanged(
          ([previousRoute, previousSearch], [currentRoute, currentSearch]) =>
            previousRoute.category === currentRoute.category &&
            previousRoute.sort === currentRoute.sort &&
            previousSearch === currentSearch,
        ),
        switchMap(([{ category, sort }, searchTerm]) => {
          this.loadingDishes = true;

          const waitForSearch = searchTerm !== appliedSearchTerm;
          const request$ = waitForSearch ? timer(this.SEARCH_DEBOUNCE_MS) : of(0);

          return request$.pipe(
            tap(() => {
              appliedSearchTerm = searchTerm;
            }),
            switchMap(() =>
              this.dishesService.getDishes(category, sort, searchTerm || undefined),
            ),
            catchError((error: unknown) => {
              console.error(error);
              return of(null);
            }),
          );
        }),
      )
      .subscribe((data) => {
        this.loadingDishes = false;
        if (data === null) {
          return;
        }

        this.dishes = data.map((dish) => ({
          ...dish,
          imageUrl: dish.imageUrl || 'assets/img/dish.png',
          name: dish.name || 'Unknown Dish',
          price: dish.price || 0,
          weight: dish.weight || 'N/A',
          description: dish.description || 'N/A',
          state: dish.state || 'N/A',
          dishType: dish.dishType || 'N/A',
          calories: dish.calories || '0',
          proteins: dish.proteins || '0',
          fats: dish.fats || '0',
          carbohydrates: dish.carbohydrates || '0',
          vitamins: dish.vitamins || '0',
        }));

        this.filteredDishes = this.dishes;
      });
  }

  ngOnDestroy(): void {
    this.menuStateSubscription?.unsubscribe();
    this.searchState.setQuery('');
  }

  onCategorySelected(category: DishType): void {
    this.selectedCategory = this.selectedCategory === category ? null : category;

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        category: this.selectedCategory || null,
        sort: this.selectedSort,
      },
      queryParamsHandling: 'merge',
    });
  }

  onSortChanged(event: Event): void {
    const selectElement = event.target as HTMLSelectElement;
    const sort = selectElement.value as DishSort;
    this.selectedSort = sort;

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        category: this.selectedCategory || null,
        sort: this.selectedSort,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  public selectedDish: DishListResponse | null = null;

  public openModal(dish: DishListResponse): void {
    this.selectedDish = dish;
  }

  public closeModal(): void {
    this.selectedDish = null;
  }

  public addDishToCart(dish: DishListResponse): void {


    this.orderService
      .addDishToPreOrderByReservationId(this.preOrderReservationId!, dish, 1)
      .subscribe({
        next: () => {
          this.toast.success('Dish added to cart successfully!', 'Success');
        },
        error: (err) => {
          this.toast.error((err as Error).message, 'Error');
        },
      });
  }
}
