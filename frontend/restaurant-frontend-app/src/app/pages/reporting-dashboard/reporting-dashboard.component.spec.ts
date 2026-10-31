import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { LocationResponse } from '@app/models/locations/location-response.model';
import { ReportRow } from '@app/models/reports/reporsts.model';
import { LocationsService } from '@app/services/locations/locations.service';
import { ReportsService } from '@app/services/reports/reports.service';
import { ReportingDashboardComponent } from './reporting-dashboard.component';

describe('ReportingDashboardComponent', () => {
  let component: ReportingDashboardComponent;
  let fixture: ComponentFixture<ReportingDashboardComponent>;
  let authentication: jasmine.SpyObj<AuthenticationService>;
  let locationsService: jasmine.SpyObj<LocationsService>;
  let reportsService: jasmine.SpyObj<ReportsService>;
  const locations: LocationResponse[] = [
    { id: 'loc-1', address: '1 Main Street' } as unknown as LocationResponse,
    { id: 'loc-2', address: '2 Oak Avenue' } as unknown as LocationResponse,
  ];

  beforeEach(async () => {
    authentication = jasmine.createSpyObj<AuthenticationService>(
      'AuthenticationService',
      ['getToken', 'getUser']
    );
    authentication.getToken.and.returnValue('id-token');
    authentication.getUser.and.returnValue({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      role: 'Admin',
    } as ReturnType<AuthenticationService['getUser']>);
    Object.defineProperty(authentication, 'user$', { value: of(authentication.getUser()) });
    locationsService = jasmine.createSpyObj<LocationsService>('LocationsService', ['getLocations']);
    locationsService.getLocations.and.returnValue(of(locations));
    reportsService = jasmine.createSpyObj<ReportsService>('ReportsService', [
      'getReportData',
      'downloadReportPdf',
      'downloadReportExcel',
      'downloadReportCsv',
    ]);

    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
        { provide: AuthenticationService, useValue: authentication },
        { provide: LocationsService, useValue: locationsService },
        { provide: ReportsService, useValue: reportsService },
      ],
      imports: [ReportingDashboardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ReportingDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the current administrator and available locations', () => {
    expect(component.firstName).toBe('Ada');
    expect(component.lastName).toBe('Lovelace');
    expect(component.role).toBe('Admin');
    expect(component.customerId).toBe('user-1');
    expect(component.locations).toEqual(locations);
    expect(component.locationAddresses).toEqual(['1 Main Street', '2 Oak Avenue']);
    expect(locationsService.getLocations).toHaveBeenCalled();
  });

  it('skips loading locations if the user has no token or profile', async () => {
    const warn = spyOn(console, 'warn');
    authentication.getToken.and.returnValue(undefined);
    await component.ngOnInit();
    expect(locationsService.getLocations).toHaveBeenCalledTimes(1);
    expect(warn).toHaveBeenCalledWith('No token found in sessionStorage');

    authentication.getToken.and.returnValue('id-token');
    authentication.getUser.and.returnValue(null);
    await component.ngOnInit();
    expect(locationsService.getLocations).toHaveBeenCalledTimes(1);
    expect(warn).toHaveBeenCalledWith('No user found in localStorage');
  });

  it('reports location loading failures', () => {
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    locationsService.getLocations.and.returnValue(throwError(() => error));
    component.ngOnInit();
    expect(logError).toHaveBeenCalledWith('Failed to load locations:', error);
  });

  it('updates and resets the selected date range', () => {
    component.onDatesUpdated({
      startDate: new Date(2026, 9, 1),
      endDate: new Date(2026, 9, 31),
    });
    expect(component.getPeriodPlaceholder()).toBe('Oct 1, 2026 – Oct 31, 2026');

    component.onDatesUpdated(null);
    expect(component.selected).toBeNull();
    expect(component.getPeriodPlaceholder()).toBe('Select period');
    component.resetDateRange();
    component.onDatesUpdated({ startDate: null, endDate: new Date() });
    expect(component.selected).toBeNull();
  });

  it('selects and clears locations and report types', () => {
    component.reportData = [{} as never];
    component.onLocationSelected('1 Main Street');
    expect(component.selectedLocationId).toBe('loc-1');
    expect(component.getSelectedLocationAddress()).toBe('1 Main Street');
    expect(component.reportData).toEqual([]);

    component.reportData = [{} as never];
    component.onLocationSelected('unknown');
    expect(component.selectedLocationId).toBeUndefined();
    expect(component.getSelectedLocationAddress()).toBeUndefined();
    expect(component.reportData).toEqual([]);

    component.onReportTypeSelected('Sales');
    expect(component.selectedReportType).toBe('Sales');
    expect(component.reportData).toEqual([]);
  });

  it('requests reports for the selected date range and report type', () => {
    const rows: ReportRow[] = [{
      locationName: 'Tasty Place',
      waiterName: 'Ada',
      waiterEmail: 'ada@example.test',
      startDate: '2026-10-01',
      endDate: '2026-10-31',
      workingHours: 8,
      totalOrders: 4,
      ordersProcessed: 4,
      deltaTotalOrders: 1,
      ordersDelta: '+1',
      avgServiceFeedback: 5,
    }];
    reportsService.getReportData.and.returnValue(of(rows));
    component.onLocationSelected('1 Main Street');
    component.onReportTypeSelected('Staff performance');
    component.onDatesUpdated({
      startDate: new Date(2026, 9, 1),
      endDate: new Date(2026, 9, 31),
    });

    component.genetateReportRequest();

    expect(reportsService.getReportData).toHaveBeenCalledWith(
      0,
      '2026-10-01',
      '2026-10-31',
      'loc-1'
    );
    expect(component.reportData).toEqual(rows);

    component.onReportTypeSelected('Sales');
    component.genetateReportRequest();
    expect(reportsService.getReportData).toHaveBeenCalledWith(
      1,
      '2026-10-01',
      '2026-10-31',
      'loc-1'
    );
  });

  it('validates required report inputs and handles report errors', () => {
    const warn = spyOn(console, 'warn');
    component.genetateReportRequest();
    expect(reportsService.getReportData).not.toHaveBeenCalled();
    expect(warn).toHaveBeenCalledWith('Missing data to generate report');

    component.onLocationSelected('1 Main Street');
    component.onReportTypeSelected('Sales');
    component.onDatesUpdated({
      startDate: new Date(2026, 9, 1),
      endDate: new Date(2026, 9, 31),
    });
    const error = new Error('offline');
    const logError = spyOn(console, 'error');
    reportsService.getReportData.and.returnValue(throwError(() => error));
    component.genetateReportRequest();
    expect(logError).toHaveBeenCalledWith('Failed to fetch report data:', error);
  });

  it('dispatches each supported download format and warns for invalid input', () => {
    component.onLocationSelected('1 Main Street');
    component.onReportTypeSelected('Sales');
    component.onDatesUpdated({
      startDate: new Date(2026, 9, 1),
      endDate: new Date(2026, 9, 31),
    });

    component.onDownloadOptionSelected('Download in PDF');
    component.onDownloadOptionSelected('Download in Excel');
    component.onDownloadOptionSelected('Download in CSV');
    expect(reportsService.downloadReportPdf).toHaveBeenCalledWith(
      1,
      '2026-10-01',
      '2026-10-31',
      'loc-1'
    );
    expect(reportsService.downloadReportExcel).toHaveBeenCalled();
    expect(reportsService.downloadReportCsv).toHaveBeenCalled();

    const warn = spyOn(console, 'warn');
    component.onDownloadOptionSelected('unknown');
    expect(warn).toHaveBeenCalledWith('Unknown download option:', 'unknown');

    component.onReportTypeSelected('');
    component.onDownloadOptionSelected('Download in PDF');
    expect(warn).toHaveBeenCalledWith('Report type or location not selected.');
  });

  it('selects dates, supports range selection and hides the calendar when complete', fakeAsync(() => {
    const start = new Date(2026, 9, 10);
    const earlier = new Date(2026, 9, 8);
    const end = new Date(2026, 9, 12);
    component.calendarVisible = true;

    component.onDayClicked(start);
    expect(component.selected?.startDate.date()).toBe(10);
    expect(component.selected?.endDate).toBeNull();
    component.onDayClicked(earlier);
    expect(component.selected?.startDate.date()).toBe(8);
    component.reportData = [{} as never];
    component.onDayClicked(end);
    expect(component.selected?.endDate?.date()).toBe(12);
    expect(component.reportData).toEqual([]);
    tick();
    expect(component.calendarVisible).toBeFalse();
  }));

  it('reports selected days and days within the range', () => {
    component.onDayClicked(new Date(2026, 9, 10));
    expect(component.isSelected(new Date(2026, 9, 10))).toBeTrue();
    expect(component.isSelected(new Date(2026, 9, 11))).toBeFalse();
    expect(component.isInRange(new Date(2026, 9, 11))).toBeFalse();

    component.onDayClicked(new Date(2026, 9, 12));
    expect(component.isSelected(new Date(2026, 9, 12))).toBeTrue();
    expect(component.isInRange(new Date(2026, 9, 11))).toBeTrue();
    expect(component.isInRange(new Date(2026, 9, 10))).toBeFalse();
    expect(component.isInvalidDate(new Date(Number.NaN))).toBeTrue();
    expect(component.isInvalidDate(new Date(2026, 9, 10))).toBeFalse();
  });

  it('builds month days and moves between months', () => {
    component.currentMonth = component.currentMonth.year(2026).month(9);
    const days = component.getDaysInMonth();
    expect(days).toHaveSize(34);
    expect(Number.isNaN(days[0].getTime())).toBeTrue();
    expect(days[3].getDate()).toBe(1);
    expect(days[33].getDate()).toBe(31);

    component.previousMonth();
    expect(component.currentMonth.month()).toBe(8);
    component.nextMonth();
    expect(component.currentMonth.month()).toBe(9);
    component.nextMonth();
    expect(component.currentMonth.month()).toBe(10);
  });

  it('toggles calendar visibility', () => {
    component.toggleCalendar();
    expect(component.calendarVisible).toBeTrue();
    component.toggleCalendar();
    expect(component.calendarVisible).toBeFalse();
  });
});
