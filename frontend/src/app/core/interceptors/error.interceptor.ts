import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { httpErrorMessage } from '../notifications/http-error';
import { ToastService } from '../notifications/toast.service';
import { SKIP_ERROR_TOAST } from './http-context';

/** Surfaces API errors as toasts and logs out when an authenticated session expires. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // A 401 only means "session expired" for an already-authenticated user.
      // For the login request itself, let the component surface the error.
      if (error.status === 401 && auth.isAuthenticated()) {
        auth.logout();
        router.navigate(['/login']);
      } else if (!req.context.get(SKIP_ERROR_TOAST)) {
        toast.error(httpErrorMessage(error));
      }
      return throwError(() => error);
    }),
  );
};
