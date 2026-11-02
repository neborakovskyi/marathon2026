import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';

import { HeaderComponent } from './header.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { NotificationsService } from '@app/services/notifications/notifications.service';
import { SearchStateService } from '@app/services/search-state.service';

describe('HeaderComponent', () => {
  let component: HeaderComponent;
  let fixture: ComponentFixture<HeaderComponent>;

  let authMock: jasmine.SpyObj<AuthenticationService>;
  let notificationsMock: jasmine.SpyObj<NotificationsService>;

  function setCurrentUrl(url: string): void {
    spyOnProperty(TestBed.inject(Router), 'url', 'get').and.returnValue(url);
  }

  function enterSearchQuery(query: string): void {
    const searchInput = fixture.nativeElement.querySelector(
      'input[type="search"]',
    ) as HTMLInputElement;

    searchInput.value = query;
    searchInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    authMock = jasmine.createSpyObj<AuthenticationService>(
      'AuthenticationService',
      ['signOut'],
    {
      user$: of(null),
      isAuthorised$: of(false),
    },
  );
  
  notificationsMock = jasmine.createSpyObj<NotificationsService>(
    'NotificationsService',
    ['loadNotifications', 'markAsRead'],
  );

  notificationsMock.loadNotifications.and.returnValue(of([]));
  notificationsMock.markAsRead.and.returnValue(of(void 0));

  await TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: 'https://test.api' },
      { provide: AuthenticationService, useValue: authMock },
      { provide: NotificationsService, useValue: notificationsMock },
      provideRouter([]),
    ],
    imports: [HeaderComponent],
  }).compileComponents();

  fixture = TestBed.createComponent(HeaderComponent);
  component = fixture.componentInstance;
  fixture.detectChanges();
});

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  ['/main', '/other'].forEach((url) => {
    it(`updates shared search state and navigates to the menu from ${url}`, () => {
      setCurrentUrl(url);
      const router = TestBed.inject(Router);
      const navigateSpy = spyOn(router, 'navigate').and.returnValue(
        Promise.resolve(true),
      );
      const searchState = TestBed.inject(SearchStateService);

      enterSearchQuery('  Pasta  ');

      expect(component.searchQuery()).toBe('  Pasta  ');
      expect(searchState.query()).toBe('  Pasta  ');
      expect(navigateSpy).toHaveBeenCalledOnceWith(['/menu']);
    });
  });

  it('updates the shared search query without navigating for whitespace', () => {
    setCurrentUrl('/main');
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(
      Promise.resolve(true),
    );
    const searchState = TestBed.inject(SearchStateService);

    enterSearchQuery('   \t ');

    expect(component.searchQuery()).toBe('   \t ');
    expect(searchState.query()).toBe('   \t ');
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('updates search state but only navigates once while navigation is pending', async () => {
    setCurrentUrl('/main');
    const router = TestBed.inject(Router);
    let resolveNavigation!: (result: boolean) => void;
    const navigation = new Promise<boolean>((resolve) => {
      resolveNavigation = resolve;
    });
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(navigation);

    enterSearchQuery('P');
    enterSearchQuery('Pa');
    enterSearchQuery('Pasta');

    expect(component.searchQuery()).toBe('Pasta');
    expect(navigateSpy).toHaveBeenCalledOnceWith(['/menu']);

    resolveNavigation(true);
    await fixture.whenStable();
  });

  it('logs rejected search navigation and clears the pending state', async () => {
    setCurrentUrl('/main');
    const router = TestBed.inject(Router);
    const navigationError = new Error('Navigation failed');
    const navigateSpy = spyOn(router, 'navigate').and.returnValues(
      Promise.reject(navigationError),
      Promise.resolve(true),
    );
    const errorSpy = spyOn(console, 'error');

    enterSearchQuery('Pasta');
    await fixture.whenStable();

    expect(errorSpy).toHaveBeenCalledOnceWith(
      'Failed to navigate to menu from search',
      navigationError,
    );

    enterSearchQuery('Pasta special');
    expect(navigateSpy).toHaveBeenCalledTimes(2);
  });

  it('does not navigate for edits or clearing while already on the menu', () => {
    setCurrentUrl('/menu?category=pasta');
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(
      Promise.resolve(true),
    );

    enterSearchQuery('Pasta');
    enterSearchQuery('Pasta special');
    enterSearchQuery('');

    expect(component.searchQuery()).toBe('');
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
