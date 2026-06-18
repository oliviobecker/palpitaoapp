import { HttpErrorResponse } from '@angular/common/http';
import { TranslateService } from '@ngx-translate/core';

/**
 * Extracts a friendly, localized message from an API error response. Network and
 * generic failures use translation keys (so they follow the selected language);
 * messages already produced by the backend (body.message / body.errors) are
 * surfaced as-is, since the server localizes them.
 */
export function httpErrorMessage(error: HttpErrorResponse, translate: TranslateService): string {
  if (error.status === 0) {
    return translate.instant('errors.network');
  }
  if (error.status === 401) {
    return translate.instant('errors.invalidCredentials');
  }

  const body = error.error;
  if (body && typeof body === 'object') {
    if (typeof body.message === 'string') {
      return body.message;
    }
    if (body.errors) {
      const first = Object.values(body.errors)[0];
      if (Array.isArray(first) && first.length) {
        return String(first[0]);
      }
    }
  }

  return translate.instant('errors.unexpected');
}
