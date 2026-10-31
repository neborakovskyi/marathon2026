import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ToastService } from '@app/services/toast/toast.service';

import { MakeReservationComponent } from './make-reservation.component';

describe('MakeReservationComponent', () => {
  const timeRange = {
    start: '2026-10-31T12:00:00Z',
    end: '2026-10-31T13:30:00Z',
  };
  const table = {
    id: 'table-1',
    tableNumber: 1,
    locationId: 'location-1',
    capacity: 4,
    timeRanges: [timeRange],
  };

  let component: MakeReservationComponent;
  let fixture: ComponentFixture<MakeReservationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
      ],
      imports: [MakeReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MakeReservationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('table', table);
    fixture.componentRef.setInput('selectedTimeRange', timeRange);
    fixture.componentRef.setInput('locationName', 'Test Restaurant');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
