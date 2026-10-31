import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { FeedbackReservationResponseItem } from '@app/models/feedbacks/feedback-reservation-response-item.model';
import { FeedbackResponseItem } from '@app/models/feedbacks/feedback-response-item.model';
import { ProfileImageResponse } from '@app/models/profile-img/profile-image-response.model';
import { FeedbacksService } from '@app/services/feedbacks/feedbacks.service';
import { ToastService } from '@app/services/toast/toast.service';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { UsersService } from '@app/services/users/users.service';
import { FeedbackWindowComponent } from './feedback-window.component';

describe('FeedbackWindowComponent', () => {
  let component: FeedbackWindowComponent;
  let fixture: ComponentFixture<FeedbackWindowComponent>;
  let feedbacks: jasmine.SpyObj<FeedbacksService>;
  let toast: jasmine.SpyObj<ToastService>;
  let auth: jasmine.SpyObj<AuthenticationService>;
  let users: jasmine.SpyObj<UsersService>;
  const reservation = (): Reservation => ({
    id: 'reservation-1',
    customerId: 'customer-1',
    waiterId: 'waiter-1',
    tableId: 'table-1',
    startTime: new Date('2026-10-17T12:00:00.000Z'),
    endTime: new Date('2026-10-17T13:30:00.000Z'),
    guestCount: 2,
    status: ReservationStatus.Reserved,
    locationAddress: '1 Main Street',
    tableCapacity: 4,
  });
  const existingFeedback = (): FeedbackReservationResponseItem => ({
    service: {
      userId: 'customer-1',
      locationId: 'loc-1',
      reservationId: 'reservation-1',
      username: 'Ada',
      rating: 4,
      comment: 'Good service',
      createdAt: '2026-10-17',
      type: 'Service',
    },
    cuisineExperience: {
      userId: 'customer-1',
      locationId: 'loc-1',
      reservationId: 'reservation-1',
      username: 'Ada',
      rating: 5,
      comment: 'Great food',
      createdAt: '2026-10-17',
      type: 'CuisineExperience',
    },
  });

  beforeEach(async () => {
    feedbacks = jasmine.createSpyObj<FeedbacksService>(
      'FeedbacksService',
      ['leaveFeedback', 'updateFeedback']
    );
    feedbacks.leaveFeedback.and.returnValue(of({} as unknown as FeedbackResponseItem));
    feedbacks.updateFeedback.and.returnValue(of('updated'));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error', 'info']);
    auth = jasmine.createSpyObj<AuthenticationService>(
      'AuthenticationService',
      ['getUserNameById$']
    );
    auth.getUserNameById$.and.returnValue(of('Ada Waiter'));
    users = jasmine.createSpyObj<UsersService>('UsersService', ['getUserProfileImage']);
    users.getUserProfileImage.and.returnValue(of({ presignedUrl: 'avatar.png' } as ProfileImageResponse));

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: FeedbacksService, useValue: feedbacks },
        { provide: ToastService, useValue: toast },
        { provide: AuthenticationService, useValue: auth },
        { provide: UsersService, useValue: users },
      ],
      imports: [FeedbackWindowComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FeedbackWindowComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('selectedReservation', reservation());
    fixture.detectChanges();
  });

  it('loads the waiter name and profile image for a reservation', () => {
    expect(component.waiterName).toBe('Ada Waiter');
    expect(component.waiterProfileImage).toBe('avatar.png');
    expect(auth.getUserNameById$).toHaveBeenCalledWith('waiter-1');
    expect(users.getUserProfileImage).toHaveBeenCalledWith('waiter-1');
    expect(component.isAnonymous).toBeFalse();
  });

  it('falls back to the default waiter image and reports waiter lookup errors', () => {
    users.getUserProfileImage.and.returnValue(
      of({ presignedUrl: null } as unknown as ProfileImageResponse)
    );
    component.ngOnInit();
    expect(component.waiterProfileImage).toBe('/assets/img/avatar.png');

    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    auth.getUserNameById$.and.returnValue(throwError(() => error));
    users.getUserProfileImage.and.returnValue(throwError(() => error));
    component.ngOnInit();
    expect(logError).toHaveBeenCalledWith('Error fetching waiter name:', error);
    expect(logError).toHaveBeenCalledWith('Error fetching waiter profile image:', error);
    expect(toast.error).toHaveBeenCalledWith('Failed to load waiter information.');
    expect(toast.error).toHaveBeenCalledWith('Failed to load waiter profile image.');
  });

  it('loads existing ratings and hides already-rated anonymous forms', () => {
    const selected = { ...reservation(), customerId: '' };
    fixture.componentRef.setInput('selectedReservation', selected);
    fixture.componentRef.setInput('feedbackForUpdate', existingFeedback());
    fixture.detectChanges();
    component.ngOnInit();

    expect(component.isAnonymous).toBeTrue();
    expect(component.serviceRating).toBe(4);
    expect(component.serviceComment).toBe('Good service');
    expect(component.cuisineRating).toBe(5);
    expect(component.cuisineComment).toBe('Great food');
    expect(component.showServiceForm).toBeFalse();
    expect(component.showCuisineForm).toBeFalse();
  });

  it('closes feedback automatically when anonymous feedback is already complete', () => {
    const closed = spyOn(component.closedFeedback, 'emit');
    fixture.componentRef.setInput('selectedReservation', { ...reservation(), customerId: null });
    fixture.componentRef.setInput('feedbackForUpdate', existingFeedback());
    component.ngOnInit();

    expect(component.showServiceForm).toBeFalse();
    expect(component.showCuisineForm).toBeFalse();
    expect(closed).toHaveBeenCalled();
  });

  it('selects the remaining anonymous feedback tab when one review already exists', () => {
    const existing = existingFeedback();
    fixture.componentRef.setInput('selectedReservation', { ...reservation(), customerId: '' });
    fixture.componentRef.setInput('feedbackForUpdate', {
      ...existing,
      cuisineExperience: null,
    } as unknown as FeedbackReservationResponseItem);
    component.ngOnInit();
    expect(component.showServiceForm).toBeFalse();
    expect(component.showCuisineForm).toBeTrue();
    expect(component.selectedTab).toBe('cuisine');
  });

  it('submits new service and cuisine reviews and emits completion events', () => {
    const selected = reservation();
    fixture.componentRef.setInput('selectedReservation', selected);
    fixture.componentRef.setInput('anonymousToken', 'anonymous-token');
    const updated = spyOn(component.feedbackUpdated, 'emit');
    const closed = spyOn(component.closedFeedback, 'emit');
    component.serviceRating = 5;
    component.serviceComment = '  Friendly service  ';
    component.cuisineRating = 4;
    component.cuisineComment = ' Nice meal ';

    component.onSubmitFeedback();

    expect(feedbacks.leaveFeedback).toHaveBeenCalledTimes(2);
    expect(feedbacks.leaveFeedback).toHaveBeenCalledWith(
      jasmine.objectContaining({
        reservationId: 'reservation-1',
        userId: 'customer-1',
        rating: 5,
        comment: 'Friendly service',
      }),
      'anonymous-token'
    );
    expect(selected.status).toBe(ReservationStatus.Finished);
    expect(updated).toHaveBeenCalledWith(jasmine.objectContaining({
      reservationId: 'reservation-1',
      feedback: jasmine.objectContaining({
        service: { rating: 5, comment: '  Friendly service  ' },
        cuisineExperience: { rating: 4, comment: ' Nice meal ' },
      }),
    }));
    expect(closed).toHaveBeenCalled();
  });

  it('skips empty reviews and does not send duplicate reviews already present', () => {
    component.serviceRating = 0;
    component.cuisineRating = 0;
    component.onSubmitFeedback();
    expect(feedbacks.leaveFeedback).not.toHaveBeenCalled();

    fixture.componentRef.setInput('feedbackForUpdate', existingFeedback());
    component.serviceRating = 4;
    component.cuisineRating = 5;
    component.onSubmitFeedback();
    expect(feedbacks.leaveFeedback).not.toHaveBeenCalled();
  });

  it('reports feedback submission failures', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    feedbacks.leaveFeedback.and.returnValue(throwError(() => error));
    component.serviceRating = 5;
    component.onSubmitFeedback();
    expect(logError).toHaveBeenCalledWith('Error submitting feedback:', error);
  });

  it('only sends changed reviews when updating existing feedback', () => {
    const closed = spyOn(component.closedFeedback, 'emit');
    const existing = existingFeedback();
    fixture.componentRef.setInput('feedbackForUpdate', existing);
    component.serviceRating = 4;
    component.serviceComment = 'Good service';
    component.cuisineRating = 5;
    component.cuisineComment = 'Improved food';
    const updated = spyOn(component.feedbackUpdated, 'emit');

    component.onUpdateFeedback();

    expect(feedbacks.updateFeedback).toHaveBeenCalledTimes(1);
    expect(feedbacks.updateFeedback).toHaveBeenCalledWith(
      jasmine.objectContaining({
        reservationId: 'reservation-1',
        rating: 5,
        comment: 'Improved food',
      })
    );
    expect(updated).toHaveBeenCalled();
    expect(closed).toHaveBeenCalled();
  });

  it('shows an informational message when there are no feedback changes', () => {
    fixture.componentRef.setInput('feedbackForUpdate', existingFeedback());
    component.serviceRating = 4;
    component.serviceComment = 'Good service';
    component.cuisineRating = 5;
    component.cuisineComment = 'Great food';

    component.onUpdateFeedback();

    expect(toast.info).toHaveBeenCalledWith('No changes to update.');
    expect(feedbacks.updateFeedback).not.toHaveBeenCalled();
  });

  it('reports failures while updating reviews', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    feedbacks.updateFeedback.and.returnValue(throwError(() => error));
    component.serviceRating = 5;
    component.serviceComment = 'Updated';

    component.onUpdateFeedback();

    expect(logError).toHaveBeenCalledWith('Error updating feedback:', error);
  });

  it('closes with a fade and extracts safe display names', () => {
    const closed = spyOn(component.closedFeedback, 'emit');
    component.onClose();
    expect(component.isFading).toBeTrue();
    expect(closed).toHaveBeenCalled();
    expect(component.getFirstName()).toBe('Unknown');
    expect(component.getFirstName('Anonymous User')).toBe('Unknown');
    expect(component.getFirstName('Ada Lovelace')).toBe('Ada');
    expect(component.getFirstName('')).toBe('Unknown');
  });
});
