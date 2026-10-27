import { Component, Output, EventEmitter, ChangeDetectionStrategy, input } from '@angular/core';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { CurrencyPipe } from '@angular/common';

@Component({
  selector: 'app-dish-modal',
  standalone: true,
  imports: [CurrencyPipe],
  templateUrl: './dish-modal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './dish-modal.component.scss',
})
export class DishModalComponent {
  readonly dish = input.required<DishListResponse>();
  @Output() closed = new EventEmitter<void>();

  onClose(): void {
    this.closed.emit();
  }
}
