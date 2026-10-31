import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimeSlotComponent } from './time-slot.component';

describe('TimeSlotComponent', () => {
  let component: TimeSlotComponent;
  let fixture: ComponentFixture<TimeSlotComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeSlotComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TimeSlotComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('timeRange', {
      start: '2026-10-31T12:00:00Z',
      end: '2026-10-31T13:30:00Z',
    });
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
