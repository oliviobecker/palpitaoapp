import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TokenStorageService } from '../auth/token-storage.service';
import { SKIP_TENANT_HEADERS } from './http-context';

/** Attaches the JWT bearer token to outgoing API requests. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.context.get(SKIP_TENANT_HEADERS)) {
    return next(req);
  }
  const token = inject(TokenStorageService).getToken();
  if (!token) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
