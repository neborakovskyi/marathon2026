import { Component, ChangeDetectionStrategy, OnInit, inject, computed } from '@angular/core';
import { RouterLink, ActivatedRoute, Router, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { LocationResponse } from '@app/models/locations/location-response.model';
import { LocationsService } from '@app/services/locations/locations.service';
import { DishesService } from '@app/services/dishes/dishes.service';
import { LocationCardComponent } from '@app/shared/custom-components/location-card/location-card.component';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { FeedbackWindowComponent } from '@app/components/reservations/feedback-window/feedback-window.component';
import {
  Reservation,
  ReservationCustomer,
  ReservationStatus,
} from '@app/models/reservations/reservation.model';
import { FeedbackReservationResponseItem } from '@app/models/feedbacks/feedback-reservation-response-item.model';
import { UsersService } from '@app/services/users/users.service';
import { FeedbacksService } from '@app/services/feedbacks/feedbacks.service';

import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs/operators';

import { LoginModalComponent } from '@app/auth/components/login/login-modal/login-modal.component';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { DishesDataSource } from './dishes.data-source';
import { DishModalComponent } from '@app/components/dish-modal/dish-modal.component';
import { DishCardComponent } from '@app/shared/custom-components/dish-card/dish-card.component';

@Component({
  selector: 'app-main-page',
  templateUrl: './main-page.component.html',
  styleUrls: ['./main-page.component.scss'],
  standalone: true,
  imports: [CommonModule, RouterLink, LocationCardComponent, FeedbackWindowComponent, LoginModalComponent, ScrollingModule, DishModalComponent, DishCardComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  providers: [LocationsService, DishesService, ReservationService, UsersService, FeedbacksService],
})
export class MainPageComponent implements OnInit {
  public dishes: DishListResponse[] = []; //remove
  public locations: LocationResponse[] = [];
  public loadingDishes = true;
  public loadingLocation = true;

  public selectedDish: DishListResponse | null = null;

  public showFeedbackModal = false;
  public selectedReservationCustomer!: ReservationCustomer;
  public feedbackForUpdate: FeedbackReservationResponseItem | undefined;
  public selectedTable = 0;
  public anonymousFeedbackToken = '';

  private readonly locationsService = inject(LocationsService);
  private readonly dishesService = inject(DishesService);
  readonly dishesDataSource = new DishesDataSource(this.dishesService);
  private readonly route = inject(ActivatedRoute);
  private readonly reservationsService = inject(ReservationService)
  private readonly userService = inject(UsersService);
  private readonly feedbackService = inject(FeedbacksService);

  private readonly router = inject(Router);

  // signals-first determination of the current URL
  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  // display the modal when URL = /login
  public readonly showLoginModal = computed(() => this.currentUrl().startsWith('/login'));

  // closing the modal returns to the main screen
  public closeLogin(): void {
    this.router.navigate(['/main']);
  }

  ngOnInit(): void {
    this.loadPopularDishes();
    this.loadLocations();

    this.route.queryParams.subscribe((params) => {
      const reservationId = params['reservationId'];
      this.anonymousFeedbackToken = params['token'] || '';
      if (reservationId) {
        this.loadReservationForFeedback(reservationId);
      }
    });
  }

  public openModal(dish: DishListResponse): void {
    this.selectedDish = dish;
  }

  public closeModal(): void {
    this.selectedDish = null;
  }

  public loadPopularDishes(): void {
    this.loadingDishes = true;
    this.dishesService.getPopularDishes(12, null).subscribe({
      next: (data) => {
        this.dishes = data.items.slice(0, 4).map((dish) => ({
          ...dish,
          imageUrl: dish.imageUrl || 'assets/img/dish.png',
          name: dish.name || 'Unknown Dish',
          price: dish.price || 0,
          weight: dish.weight || 'N/A',
        }));
        this.loadingDishes = false;
      },
      error: (err) => {
        console.error(err);
        this.loadingDishes = false;
      }
    });
  }
  // virtual scroll
  // loadPopularDishes(): void {
  //   this.dishesDataSource.loadNextPage();
  // }

  // onDishScroll(index: number): void {
  //   if (index >= this.dishesDataSource.data.length - 5) {
  //     this.dishesDataSource.loadNextPage();
  //   }
  // }

  // trackDish(_index: number, dish: DishListResponse): string {
  //   return dish.id;
  // }

  loadLocations(): void {
    this.loadingLocation = true;
    this.locationsService.getLocations().subscribe({
      next: (data) => {
        this.locations = data//.slice(0, 3)
        .map((location) => ({
          ...location,
          imageUrl: location.imageUrl || 'assets/img/locations/baratashvili-street-14.jpg',
          address: location.address || 'Unknown Address',
          totalCapacity: location.totalCapacity || 0,
          averageOccupancy: location.averageOccupancy || 'N/A',
        }));
        this.loadingLocation = false;
      },
      error: (err) => {
        console.error(err);
        this.loadingLocation = false;
      }
    });
  }

  waiterAvatarUrl = '';

  loadReservationForFeedback(reservationId: string) {
    this.reservationsService.getReservationById(reservationId).subscribe((reservation) => {
      this.selectedReservationCustomer = reservation;

      const waiterId = reservation.waiterId;

      this.userService.getUserProfileImage(waiterId).subscribe((response) => {
        this.waiterAvatarUrl = response.presignedUrl;
      });

      this.feedbackService.getFeedbackByReservation(reservationId).subscribe((feedback) => {
        this.feedbackForUpdate = feedback;
        this.selectedReservationCustomer.feedback = feedback;

        const isAnonymous = reservation.customerId === null;
        const hasServiceFeedback = !!feedback?.service;
        const hasCuisineFeedback = !!feedback?.cuisineExperience;

        // Don't open modal at all if both feedbacks exist and user is anonymous
        if (isAnonymous && hasServiceFeedback && hasCuisineFeedback) {
          return;
        }

        this.showFeedbackModal = true;
      });
    });
  }

  get selectedReservationAsReservation(): Reservation {
    return {
      id: this.selectedReservationCustomer.id,
      tableId: this.selectedReservationCustomer.tableId,
      startTime: this.selectedReservationCustomer.startTime,
      endTime: this.selectedReservationCustomer.endTime,
      guestCount: this.selectedReservationCustomer.guestCount,
      status: this.selectedReservationCustomer.status as unknown as ReservationStatus,
      waiterId: this.selectedReservationCustomer.waiterId,
      customerId: '',
      locationAddress: this.selectedReservationCustomer.locationAddress,
      tableCapacity: this.selectedTable ?? this.selectedReservationCustomer.guestCount,
      restaurantName: this.selectedReservationCustomer.restaurantName,
    };
  }
}
