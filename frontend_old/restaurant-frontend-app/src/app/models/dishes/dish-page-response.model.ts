import { DishListResponse } from './dish-list-response.model';

export interface DishPageResponse {
  items: DishListResponse[];
  nextPageCursor: string | null;
}
