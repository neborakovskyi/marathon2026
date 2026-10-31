import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ProfileImageResponse } from '@app/models/profile-img/profile-image-response.model';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let service: UsersService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(UsersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('gets a user profile image', () => {
    const image: ProfileImageResponse = { presignedUrl: 'https://cdn.test/avatar.png' };
    service.getUserProfileImage('user-1').subscribe((response) => expect(response).toEqual(image));
    const request = httpMock.expectOne('https://test.api/users/user-1/profile-image');
    expect(request.request.method).toBe('GET');
    request.flush(image);
  });

  it('searches users by full name', () => {
    service.getUsers('Ada Lovelace').subscribe((users) => expect(users).toEqual([]));
    const request = httpMock.expectOne('https://test.api/users?fullName=Ada%20Lovelace');
    expect(request.request.params.get('fullName')).toBe('Ada Lovelace');
    request.flush([]);
  });

  it('maps a user name response to a string', () => {
    service.getUserName('user-1').subscribe((name) => expect(name).toBe('Ada Lovelace'));
    httpMock.expectOne('https://test.api/users/user-1/name').flush({ name: 'Ada Lovelace' });
  });
});
