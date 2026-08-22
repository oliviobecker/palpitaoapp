import { HttpContextToken } from '@angular/common/http';

/** When set on a request, the error interceptor will not show an error toast. */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/**
 * When set, the error interceptor will not attempt a token refresh + retry on a 401.
 * Used by the auth endpoints themselves (login/refresh/logout) and by the one retry
 * after a refresh, so a failing refresh can't trigger an infinite refresh loop.
 */
export const SKIP_AUTH_REFRESH = new HttpContextToken<boolean>(() => false);

/**
 * When set, the request carries neither the session token nor the current group.
 * Key-addressed public endpoints resolve their own tenant from the route, and a
 * logged-in browser would otherwise attach a group that scopes their data away.
 */
export const SKIP_TENANT_HEADERS = new HttpContextToken<boolean>(() => false);
