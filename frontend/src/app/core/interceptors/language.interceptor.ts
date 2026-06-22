import { HttpInterceptorFn } from '@angular/common/http';
import { storedLanguage } from '../i18n/language.service';

/**
 * Sends the current UI language to the API via the Accept-Language header.
 * Reads the persisted language directly (not via LanguageService) to avoid a DI
 * cycle: LanguageService -> TranslateService -> i18n HTTP request -> this
 * interceptor -> LanguageService.
 */
export const languageInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ setHeaders: { 'Accept-Language': storedLanguage() } }));
