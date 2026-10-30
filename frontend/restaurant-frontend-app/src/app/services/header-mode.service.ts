import { Injectable, signal } from '@angular/core';

/**
 * Позволяет страницам переключать header в тёмный/прозрачный режим.
 * Использование: inject в ngOnInit / ngOnDestroy страницы.
 */
@Injectable({ providedIn: 'root' })
export class HeaderModeService {
  readonly isDark = signal(false);

  setDark(value: boolean): void {
    this.isDark.set(value);
  }
}
