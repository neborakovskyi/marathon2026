import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { FeedbackResponseItem } from '@app/models/feedbacks/feedback-response-item.model';
import { FeedbackResponse } from '@app/models/feedbacks/feedback-response.model';
import { ReviewSort } from '@app/models/enums/review-sort.model';
import { ReviewType } from '@app/models/enums/review-type.model';
import { LocationResponse } from '@app/models/locations/location-response.model';
import { ProfileImageResponse } from '@app/models/profile-img/profile-image-response.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { FeedbacksService } from '@app/services/feedbacks/feedbacks.service';
import { LocationsService } from '@app/services/locations/locations.service';
import { ToastService } from '@app/services/toast/toast.service';
import { UsersService } from '@app/services/users/users.service';
import { LocationOverviewComponent } from './location-overview.component';

describe('LocationOverviewComponent', () => {
  let component: LocationOverviewComponent;
  let fixture: ComponentFixture<LocationOverviewComponent>;
  let locations: jasmine.SpyObj<LocationsService>;
  let feedbacks: jasmine.SpyObj<FeedbacksService>;
  let users: jasmine.SpyObj<UsersService>;
  let toast: jasmine.SpyObj<ToastService>;
  const location = {
    id: 'loc-1',
    address: '1 Main Street',
  } as unknown as LocationResponse;
  const review: FeedbackResponseItem = {
    userId: 'user-1',
    locationId: 'loc-1',
    reservationId: 'reservation-1',
    username: 'Ada',
    rating: 5,
    comment: 'Excellent',
    createdAt: '2026-10-17',
    type: 'Service',
  };
  const feedbackPage = (items: FeedbackResponseItem[], totalPages = 4): FeedbackResponse => ({
    items,
    totalPages,
    currentPage: 1,
  });

  beforeEach(async () => {
    locations = jasmine.createSpyObj<LocationsService>('LocationsService', ['getLocationById']);
    locations.getLocationById.and.returnValue(of(location));
    feedbacks = jasmine.createSpyObj<FeedbacksService>('FeedbacksService', ['getLocationFeedbacks']);
    feedbacks.getLocationFeedbacks.and.returnValue(of(feedbackPage([review])));
    users = jasmine.createSpyObj<UsersService>('UsersService', ['getUserProfileImage']);
    users.getUserProfileImage.and.returnValue(of({ presignedUrl: 'avatar.png' } as ProfileImageResponse));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error']);

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'loc-1' } } },
        },
        {
          provide: AuthenticationService,
          useValue: jasmine.createSpyObj(
            'AuthenticationService',
            ['signOut'],
            { user$: of(null) }
          ),
        },
        { provide: FeedbacksService, useValue: feedbacks },
        { provide: UsersService, useValue: users },
        { provide: ToastService, useValue: toast },
      ],
      imports: [LocationOverviewComponent],
    });
    TestBed.overrideComponent(LocationOverviewComponent, {
      remove: { providers: [LocationsService] },
      add: { providers: [{ provide: LocationsService, useValue: locations }] },
    });
    await TestBed.compileComponents();
    fixture = TestBed.createComponent(LocationOverviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the selected location, reviews and reviewer image', () => {
    expect(component.locationId).toBe('loc-1');
    expect(component.location).toBe(location);
    expect(component.reviews).toEqual([review]);
    expect(component.userImages['user-1']).toBe('avatar.png');
    expect(component.totalPages).toBe(4);
    expect(feedbacks.getLocationFeedbacks).toHaveBeenCalledWith(
      'loc-1',
      ReviewType.Service,
      ReviewSort.TopRatedFirst,
      1
    );
  });

  it('shows an empty state and fallback avatar when there are no reviews or images', () => {
    feedbacks.getLocationFeedbacks.and.returnValue(of(feedbackPage([])));
    component.loadReviews();
    expect(component.reviews).toEqual([]);
    expect(component.reviewsLabel).toBe('No reviews of this type found for this location.');

    feedbacks.getLocationFeedbacks.and.returnValue(of(feedbackPage([review])));
    users.getUserProfileImage.and.returnValue(throwError(() => new Error('offline')));
    component.userImages = {};
    component.loadReviews();
    expect(component.userImages['user-1']).toBe('assets/img/avatar.png');
  });

  it('reports location and review request errors', () => {
    locations.getLocationById.and.returnValue(throwError(() => new Error('offline')));
    feedbacks.getLocationFeedbacks.and.returnValue(throwError(() => new Error('offline')));
    component.loadLocationData();
    component.loadReviews();

    expect(toast.error).toHaveBeenCalledWith('Failed to load location details');
    expect(toast.error).toHaveBeenCalledWith('Failed to load reviews');
    expect(component.reviewsLabel).toBe('Failed to load reviews.');
  });

  it('reloads reviews after selecting a tab or sort order', () => {
    component.currentPage = 3;
    component.onTabChange(ReviewType.CuisineExperience);
    expect(component.selectedType).toBe(ReviewType.CuisineExperience);
    expect(component.currentPage).toBe(1);

    const select = document.createElement('select');
    const option = document.createElement('option');
    option.value = ReviewSort.LowRatedFirst;
    select.append(option);
    select.value = ReviewSort.LowRatedFirst;
    component.currentPage = 2;
    component.onSortChange(select);
    expect(component.selectedSort).toBe(ReviewSort.LowRatedFirst);
    expect(component.currentPage).toBe(1);

    component.currentPage = 2;
    component.onSortChange(null);
    expect(component.currentPage).toBe(2);
  });

  it('moves between pages within bounds and supports direct page selection', () => {
    expect(component.visiblePageNumbers).toEqual([1, 2, 3]);
    component.nextPage();
    expect(component.currentPage).toBe(2);
    component.goToPage(4);
    expect(component.currentPage).toBe(4);
    expect(component.visiblePageNumbers).toEqual([4]);
    component.nextPage();
    expect(component.currentPage).toBe(4);

    component.prevPage();
    expect(component.currentPage).toBe(3);
    component.prevPage();
    component.prevPage();
    component.prevPage();
    expect(component.currentPage).toBe(1);
    expect(component.getFeedbackUserImage('user-1')).toBe('avatar.png');
  });

  it('does not request duplicate reviewer images for the same user', () => {
    component.userImages['user-1'] = 'cached.png';
    component.loadReviews();
    expect(users.getUserProfileImage).toHaveBeenCalledTimes(1);
    expect(component.getFeedbackUserImage('user-1')).toBe('cached.png');
  });
});
