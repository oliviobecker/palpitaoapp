import { Injectable, signal } from '@angular/core';

export const THEME_STORAGE_KEY = 'palpitao.theme';
export type Theme = 'light' | 'dark';

/** The explicit user override, or null when none was chosen (follow the OS). */
export function storedTheme(): Theme | null {
  const v = localStorage.getItem(THEME_STORAGE_KEY);
  return v === 'light' || v === 'dark' ? v : null;
}

/**
 * Light/dark theme, applied via Bootstrap's `data-bs-theme` on the document root
 * (so Bootstrap components and our own CSS-variable tokens both flip). Mirrors
 * LanguageService: a signal + init() called once at startup. When the user has
 * not chosen explicitly we follow the OS and keep following it live.
 *
 * Note: the initial attribute is also set by an inline script in index.html to
 * avoid a flash of the light theme before Angular boots.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly current = signal<Theme>('light');

  private readonly media =
    typeof window !== 'undefined' && window.matchMedia
      ? window.matchMedia('(prefers-color-scheme: dark)')
      : null;

  init(): void {
    const saved = storedTheme();
    this.apply(saved ?? (this.media?.matches ? 'dark' : 'light'));
    // Keep following the OS while the user has not made an explicit choice.
    this.media?.addEventListener('change', (e) => {
      if (!storedTheme()) {
        this.apply(e.matches ? 'dark' : 'light');
      }
    });
  }

  /** Flips the theme and persists the choice (stops following the OS). */
  toggle(): void {
    const next: Theme = this.current() === 'dark' ? 'light' : 'dark';
    localStorage.setItem(THEME_STORAGE_KEY, next);
    this.apply(next);
  }

  private apply(theme: Theme): void {
    this.current.set(theme);
    document.documentElement.setAttribute('data-bs-theme', theme);
  }
}
