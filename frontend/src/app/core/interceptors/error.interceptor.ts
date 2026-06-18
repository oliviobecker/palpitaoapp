import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { httpErrorMessage } from '../notifications/http-error';
import { ToastService } from '../notifications/toast.service';
import { SKIP_AUTH_REFRESH, SKIP_ERROR_TOAST } from './http-context';

/**
 * On a 401 for an authenticated request, transparently refreshes the access token
 * and retries once; if the refresh fails the session is ended (logout + redirect).
 * Other errors surface as toasts. The auth endpoints opt out via SKIP_AUTH_REFRESH
 * so a failing refresh can't loop.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const auth = inject(AuthService);
  const router = inject(Router);
  const translate = inject(TranslateService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const canRefresh =
        error.status === 401 && auth.isAuthenticated() && !req.context.get(SKIP_AUTH_REFRESH);

      if (canRefresh) {
        return auth.refreshToken().pipe(
          // Retry the original request once with the fresh token. Marking it
          // SKIP_AUTH_REFRESH means a second 401 ends the session instead of looping.
          switchMap((newToken) =>
            next(
              req.clone({
                setHeaders: { Authorization: `Bearer ${newToken}` },
                context: req.context.set(SKIP_AUTH_REFRESH, true),
              }),
            ),
          ),
          catchError(() => {
            auth.logout();
            router.navigate(['/login']);
            return throwError(() => error);
          }),
        );
      }

      if (!req.context.get(SKIP_ERROR_TOAST)) {
        toast.error(httpErrorMessage(error, translate));
      }
      return throwError(() => error);
    }),
  );
};
