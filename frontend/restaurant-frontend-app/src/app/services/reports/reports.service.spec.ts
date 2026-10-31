import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReportsService } from './reports.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('ReportsService', () => {
  let service: ReportsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(ReportsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('gets report data with the selected filters', () => {
    service.getReportData(2, '2026-10-01', '2026-10-31', 'loc-1').subscribe();

    const request = httpMock.expectOne(
      'https://test.api/reports/generate?type=2&startDate=2026-10-01&endDate=2026-10-31&locationId=loc-1'
    );
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('downloads CSV content with a date-based filename', () => {
    const createUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:csv');
    const revokeUrl = spyOn(URL, 'revokeObjectURL');
    const click = spyOn(HTMLAnchorElement.prototype, 'click');

    service.downloadReportCsv(1, '2026-10-01', '2026-10-31', 'loc-1');

    const request = httpMock.expectOne(
      'https://test.api/reports/download?type=1&startDate=2026-10-01&endDate=2026-10-31&locationId=loc-1&format=csv'
    );
    expect(request.request.responseType).toBe('text');
    request.flush('id,total\n1,42');

    expect(createUrl).toHaveBeenCalled();
    expect(click).toHaveBeenCalled();
    expect(revokeUrl).toHaveBeenCalledWith('blob:csv');
  });

  it('downloads PDF and Excel blobs with their expected names', () => {
    const createUrl = spyOn(URL, 'createObjectURL').and.returnValues('blob:pdf', 'blob:xls');
    const revokeUrl = spyOn(URL, 'revokeObjectURL');
    const click = spyOn(HTMLAnchorElement.prototype, 'click');
    const blob = new Blob(['report']);

    service.downloadReportPdf(1, '2026-10-01', '2026-10-31', 'loc-1');
    const pdf = httpMock.expectOne(
      'https://test.api/reports/download?type=1&startDate=2026-10-01&endDate=2026-10-31&locationId=loc-1&format=pdf'
    );
    expect(pdf.request.responseType).toBe('blob');
    pdf.flush(blob);

    service.downloadReportExcel(3, '2026-09-01', '2026-09-30', 'loc-2');
    const excel = httpMock.expectOne(
      'https://test.api/reports/download?type=3&startDate=2026-09-01&endDate=2026-09-30&locationId=loc-2&format=excel'
    );
    expect(excel.request.responseType).toBe('blob');
    excel.flush(blob);

    expect(createUrl).toHaveBeenCalledTimes(2);
    expect(click).toHaveBeenCalledTimes(2);
    expect(revokeUrl).toHaveBeenCalledWith('blob:pdf');
    expect(revokeUrl).toHaveBeenCalledWith('blob:xls');
  });

  it('logs download errors without creating a download link', () => {
    const logError = spyOn(console, 'error');
    const createUrl = spyOn(URL, 'createObjectURL');

    service.downloadReportCsv(1, '2026-10-01', '2026-10-31', 'loc-1');
    httpMock
      .expectOne((request) => request.url.includes('/download?'))
      .flush('failed', { status: 500, statusText: 'Server Error' });

    expect(logError).toHaveBeenCalledWith('Error downloading Excel report:', jasmine.anything());
    expect(createUrl).not.toHaveBeenCalled();

    service.downloadReportPdf(1, '2026-10-01', '2026-10-31', 'loc-1');
    httpMock
      .expectOne((request) => request.url.includes('format=pdf'))
      .flush(new Blob(['failed']), { status: 500, statusText: 'Server Error' });
    expect(logError).toHaveBeenCalledWith('Error downloading PDF report:', jasmine.anything());

    service.downloadReportExcel(1, '2026-10-01', '2026-10-31', 'loc-1');
    httpMock
      .expectOne((request) => request.url.includes('format=excel'))
      .flush(new Blob(['failed']), { status: 500, statusText: 'Server Error' });
    expect(logError).toHaveBeenCalledWith('Error downloading Excel report:', jasmine.anything());
  });
});
