import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { GroupContextService } from '../services/group-context.service';
import { AuthService } from './auth.service';

/** Requires an authenticated user; otherwise redirects to /login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

/**
 * Requires an authenticated user with a selected group. Without a current group
 * (e.g. after login with multiple groups) the user is sent to /select-group.
 */
export const participantGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const group = inject(GroupContextService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }
  return group.hasGroup() ? true : router.createUrlTree(['/select-group']);
};

/**
 * Requires the user to be a GroupAdmin of the current group; participants are
 * redirected to the dashboard, and users without a group to /select-group.
 */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const group = inject(GroupContextService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }
  if (!group.hasGroup()) {
    return router.createUrlTree(['/select-group']);
  }
  return group.isGroupAdmin() ? true : router.createUrlTree(['/dashboard']);
};
