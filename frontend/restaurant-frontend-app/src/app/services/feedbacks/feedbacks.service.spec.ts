import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FeedbacksService } from './feedbacks.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReviewSort } from '@app/models/enums/review-sort.model';
import { ReviewType } from '@app/models/enums/review-type.model';
import { CreateFeedbackRequest } from '@app/models/feedbacks/create-feedback-request.model';
import { UpdateFeedbackRequest } from '@app/models/feedbacks/update-feedback-request.model';

describe('FeedbacksService', () => {
  let service: FeedbacksService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(FeedbacksService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('creates feedback and attaches an anonymous token when supplied', () => {
    const requestBody: CreateFeedbackRequest = {
      reservationId: 'reservation-1',
      userId: 'guest',
      rating: 5,
      comment: 'Great',
      type: ReviewType.Service,
    };
    service.leaveFeedback(requestBody, 'guest-token').subscribe();
    const request = httpMock.expectOne('https://test.api/feedback');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ ...requestBody, token: 'guest-token' });
    request.flush({});

    const authenticatedBody: CreateFeedbackRequest = { ...requestBody };
    service.leaveFeedback(authenticatedBody, '').subscribe();
    const authenticated = httpMock.expectOne('https://test.api/feedback');
    expect(authenticated.request.body).toEqual(requestBody);
    authenticated.flush({});
  });

  it('updates feedback', () => {
    const update: UpdateFeedbackRequest = {
      reservationId: 'reservation-1',
      rating: 4,
      comment: 'Updated',
      type: ReviewType.Service,
    } as UpdateFeedbackRequest;
    service.updateFeedback(update).subscribe();
    const request = httpMock.expectOne('https://test.api/feedback');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    request.flush('updated');
  });

  it('requests filtered feedback with the requested page', () => {
    service
      .getLocationFeedbacks('loc-1', ReviewType.Service, ReviewSort.NewestFirst, 3)
      .subscribe();
    const request = httpMock.expectOne('https://test.api/feedback?locationId=loc-1&type=0&sort=NewestFirst&page=3');
    expect(request.request.method).toBe('GET');
    request.flush({});

    service
      .getLocationFeedbacks('loc-1', ReviewType.CuisineExperience, ReviewSort.TopRatedFirst)
      .subscribe();
    const defaultPage = httpMock.expectOne(
      'https://test.api/feedback?locationId=loc-1&type=1&sort=TopRatedFirst&page=1'
    );
    defaultPage.flush({});
  });

  it('gets feedback for a reservation', () => {
    service.getFeedbackByReservation('reservation-1').subscribe();
    const request = httpMock.expectOne(
      'https://test.api/reservations/reservation-1/feedback'
    );
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});
