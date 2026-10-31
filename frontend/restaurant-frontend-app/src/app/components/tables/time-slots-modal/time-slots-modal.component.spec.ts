import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimeSlotsModalComponent } from './time-slots-modal.component';

describe('TimeSlotsModalComponent', () => {
  let component: TimeSlotsModalComponent;
  let fixture: ComponentFixture<TimeSlotsModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeSlotsModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TimeSlotsModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('locationName', 'Test Restaurant');
    fixture.componentRef.setInput('date', '2026-10-31T00:00:00Z');
    fixture.componentRef.setInput('table', {
      id: 'table-1',
      tableNumber: 1,
      locationId: 'location-1',
      capacity: 4,
      timeRanges: [
        {
          start: '2026-10-31T12:00:00Z',
          end: '2026-10-31T13:30:00Z',
        },
      ],
    });
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
