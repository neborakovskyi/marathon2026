import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DishSelectCardComponent } from './dish-select-card.component';

describe('DishSelectCardComponent', () => {
  const dish = {
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
  };

  let component: DishSelectCardComponent;
  let fixture: ComponentFixture<DishSelectCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishSelectCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishSelectCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('dish', dish);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
