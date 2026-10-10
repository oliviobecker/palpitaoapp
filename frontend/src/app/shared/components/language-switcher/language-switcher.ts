import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Lang, LanguageService } from '@core/i18n/language.service';

/**
 * The PT/EN toggle of the pages outside the app shell (sign-up, create group, the public
 * standings link): a small Bootstrap button group. The shell and the landing page keep their own
 * switchers, which are part of their designs.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-language-switcher',
  imports: [TranslatePipe],
  template: `
    <div
      class="btn-group btn-group-sm"
      role="group"
      [attr.aria-label]="'language.label' | translate"
    >
      @for (option of options; track option.lang) {
        <button
          type="button"
          class="btn btn-outline-secondary"
          [class.active]="language.current() === option.lang"
          (click)="language.use(option.lang)"
        >
          {{ option.label }}
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);
  protected readonly options: readonly { lang: Lang; label: string }[] = [
    { lang: 'pt-BR', label: 'PT' },
    { lang: 'en-US', label: 'EN' },
  ];
}
