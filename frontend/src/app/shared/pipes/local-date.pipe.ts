import { formatDate } from '@angular/common';
import { ChangeDetectorRef, Pipe, PipeTransform, effect, inject } from '@angular/core';
import { LanguageService } from '@core/i18n/language.service';

/**
 * Angular's `date` pipe, in the active language: `{{ when | localDate: 'EEE, dd/MM HH:mm' }}` reads
 * "Mon, 12/05" in English and "seg., 12/05" in Portuguese. The built-in pipe takes the app-wide
 * LOCALE_ID, which is fixed at startup, so weekday and month names stayed Portuguese after a
 * switch to English. Impure, and marks its view for check when the language changes, so an
 * OnPush screen follows the switch.
 */
@Pipe({ name: 'localDate', pure: false })
export class LocalDatePipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  constructor() {
    const view = inject(ChangeDetectorRef);
    effect(() => {
      this.language.current();
      view.markForCheck();
    });
  }

  transform(
    value: string | number | Date | null | undefined,
    format = 'mediumDate',
    timezone?: string,
  ): string | null {
    if (value == null || value === '') {
      return null;
    }
    return formatDate(value, format, this.language.current(), timezone);
  }
}
