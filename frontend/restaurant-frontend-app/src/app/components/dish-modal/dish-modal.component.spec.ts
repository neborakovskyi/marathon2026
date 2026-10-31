import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DishModalComponent } from './dish-modal.component';

describe('DishModalComponent', () => {
  let component: DishModalComponent;
  let fixture: ComponentFixture<DishModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('dish', {
      id: 'dish-1',
      name: 'Caesar Salad',
      imageUrl: '/assets/caesar.png',
      price: 12,
      state: 'Available',
      weight: '250 g',
      dishType: 'Main',
      description: 'Fresh salad',
      calories: '320',
      proteins: '12',
      carbohydrates: '25',
      fats: '18',
      vitamins: 'A, C',
    });
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
