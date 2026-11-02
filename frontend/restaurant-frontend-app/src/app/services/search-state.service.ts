import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SearchStateService {
  public readonly query = signal('');

  setQuery(query: string): void {
    this.query.set(query);
  }
}
