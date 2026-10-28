import { DataSource } from '@angular/cdk/collections';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishesService } from '@app/services/dishes/dishes.service';
import { BehaviorSubject, Observable, finalize } from 'rxjs';

export class DishesDataSource extends DataSource<DishListResponse> {
  private readonly dishesSubject = new BehaviorSubject<DishListResponse[]>([]);
  readonly isLoading = new BehaviorSubject(false);
  readonly error = new BehaviorSubject<string | null>(null);
  private cursor: string | null = null;
  private hasLoadedPage = false;
  private hasMore = true;
  private readonly pageSize = 12;

  constructor(private readonly dishesService: DishesService) {
    super();
  }

  get data(): DishListResponse[] {
    return this.dishesSubject.value;
  }

  connect(): Observable<DishListResponse[]> {
    return this.dishesSubject.asObservable();
  }

  disconnect(): void {
    this.dishesSubject.complete();
    this.isLoading.complete();
    this.error.complete();
  }

  loadNextPage(): void {
    if (this.isLoading.value || !this.hasMore) return;

    this.isLoading.next(true);
    this.error.next(null);
    this.dishesService.getPopularDishes(
      this.pageSize,
      this.hasLoadedPage ? this.cursor : null,
    ).pipe(finalize(() => this.isLoading.next(false))).subscribe({
      next: (page) => {
        const normalized = (Array.isArray(page.items) ? page.items : []).map((dish) => ({
          ...dish,
          imageUrl: dish.imageUrl || 'assets/img/dish.png',
          name: dish.name || 'Unknown Dish',
          price: dish.price || 0,
          weight: dish.weight || 'N/A',
        }));
        this.dishesSubject.next([...this.dishesSubject.value, ...normalized]);
        this.cursor = page.nextPageCursor;
        this.hasLoadedPage = true;
        this.hasMore = this.cursor !== null && this.cursor !== undefined;
      },
      error: () => this.error.next('Could not load dishes. Please try again.'),
    });
  }
}
