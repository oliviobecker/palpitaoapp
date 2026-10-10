import { HttpInterceptorFn } from '@angular/common/http';
import { storedGroupId } from '../services/group-context.service';
import { isApiRequest } from './api-url';
import { SKIP_TENANT_HEADERS } from './http-context';

/**
 * Sends the current group to the API via the X-Group-Id header so the backend can
 * scope every request to that tenant. Reads storage directly (not the service) to
 * mirror the language interceptor and avoid any DI ordering concerns. The backend
 * always re-validates that the user really has access to the group.
 */
export const groupInterceptor: HttpInterceptorFn = (req, next) => {
  // Never to another host, and never on a request that opted out (the public standings link).
  if (req.context.get(SKIP_TENANT_HEADERS) || !isApiRequest(req.url)) {
    return next(req);
  }
  const groupId = storedGroupId();
  if (!groupId) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'X-Group-Id': groupId } }));
};
