import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { take } from 'rxjs';

export const LANG_STORAGE_KEY = 'palpitao.lang';
export type Lang = 'pt-BR' | 'en-US';

/** Pure language selection: saved override > browser language > English fallback. */
export function pickLanguage(navLang: string | undefined | null, saved: string | null): Lang {
  if (saved === 'pt-BR' || saved === 'en-US') {
    return saved;
  }
  const n = (navLang ?? 'en').toLowerCase();
  if (n.startsWith('pt')) {
    return 'pt-BR';
  }
  return 'en-US';
}

/**
 * The persisted language, read directly from storage. Used by the language
 * interceptor so it does not depend on LanguageService (which depends on
 * TranslateService), which would create a DI cycle on the i18n HTTP requests.
 */
export function storedLanguage(): Lang {
  const saved = localStorage.getItem(LANG_STORAGE_KEY);
  return saved === 'pt-BR' || saved === 'en-US' ? saved : 'en-US';
}

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);
  readonly current = signal<Lang>('en-US');

  /** Detects and applies the initial language. Called once at app startup. */
  init(): void {
    this.translate.setFallbackLang('en-US');
    const lang = pickLanguage(navigator.language, localStorage.getItem(LANG_STORAGE_KEY));
    this.use(lang);
  }

  use(lang: Lang): void {
    // Persist before triggering the load so the interceptor sees the new value.
    localStorage.setItem(LANG_STORAGE_KEY, lang);
    this.translate.use(lang);
    this.current.set(lang);
    document.documentElement.lang = lang;
    // Keep the document title on the product name in the active language
    // (Palpitão in pt-BR, FanPicks in en-US). Resolves once translations load.
    this.translate
      .get('app.name')
      .pipe(take(1))
      .subscribe((name: string) => {
        if (name && name !== 'app.name') {
          document.title = name;
        }
      });
  }
}
