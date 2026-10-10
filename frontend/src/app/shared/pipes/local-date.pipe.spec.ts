import { registerLocaleData } from '@angular/common';
import localeEn from '@angular/common/locales/en';
import localePt from '@angular/common/locales/pt';
import { ChangeDetectorRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { LanguageService } from '@core/i18n/language.service';
import { LocalDatePipe } from './local-date.pipe';

describe('LocalDatePipe', () => {
  let language: LanguageService;
  let pipe: LocalDatePipe;

  beforeAll(() => {
    registerLocaleData(localePt);
    registerLocaleData(localeEn);
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        LocalDatePipe,
        { provide: ChangeDetectorRef, useValue: { markForCheck: () => undefined } },
        {
          provide: TranslateService,
          useValue: {
            use: () => undefined,
            setFallbackLang: () => undefined,
            get: () => ({ pipe: () => ({ subscribe: () => undefined }) }),
          },
        },
      ],
    });
    language = TestBed.inject(LanguageService);
    pipe = TestBed.runInInjectionContext(() => new LocalDatePipe());
  });

  const MONDAY = '2026-05-11T15:30:00Z';

  it('names the weekday in the active language', () => {
    language.current.set('en-US');
    expect(pipe.transform(MONDAY, 'EEE, dd/MM HH:mm', 'UTC')).toBe('Mon, 11/05 15:30');

    language.current.set('pt-BR');
    expect(pipe.transform(MONDAY, 'EEE, dd/MM HH:mm', 'UTC')).toBe('seg., 11/05 15:30');
  });

  it('formats the default medium date per language', () => {
    language.current.set('en-US');
    expect(pipe.transform(MONDAY, undefined, 'UTC')).toBe('May 11, 2026');

    language.current.set('pt-BR');
    expect(pipe.transform(MONDAY, undefined, 'UTC')).toBe('11 de mai. de 2026');
  });

  it('renders nothing for a missing date', () => {
    expect(pipe.transform(null)).toBeNull();
    expect(pipe.transform('')).toBeNull();
  });
});
