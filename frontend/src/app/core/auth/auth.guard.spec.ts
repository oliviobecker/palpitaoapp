import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { GroupContextService } from '../services/group-context.service';
import { adminGuard, authGuard, participantGuard } from './auth.guard';
import { AuthService } from './auth.service';

interface GuardState {
  authed: boolean;
  hasGroup: boolean;
  isAdmin: boolean;
}

/** Configures the injector with stubbed auth/group/router so the guards run in isolation. */
function configure(state: GuardState): void {
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { isAuthenticated: () => state.authed } },
      {
        provide: GroupContextService,
        useValue: { hasGroup: () => state.hasGroup, isGroupAdmin: () => state.isAdmin },
      },
      // createUrlTree returns a tagged object so a redirect is asserted by its commands.
      {
        provide: Router,
        useValue: { createUrlTree: (commands: unknown[]) => ({ commands }) as unknown as UrlTree },
      },
    ],
  });
}

const route = {} as never;
const snapshot = {} as never;
const run = <T>(fn: () => T): T => TestBed.runInInjectionContext(fn);

describe('route guards', () => {
  beforeEach(() => TestBed.resetTestingModule());

  describe('authGuard', () => {
    it('allows an authenticated user', () => {
      configure({ authed: true, hasGroup: true, isAdmin: true });
      expect(run(() => authGuard(route, snapshot))).toBe(true);
    });

    it('redirects an anonymous user to /login', () => {
      configure({ authed: false, hasGroup: false, isAdmin: false });
      expect(run(() => authGuard(route, snapshot))).toEqual({ commands: ['/login'] });
    });
  });

  describe('participantGuard', () => {
    it('redirects to /login when not authenticated', () => {
      configure({ authed: false, hasGroup: true, isAdmin: true });
      expect(run(() => participantGuard(route, snapshot))).toEqual({ commands: ['/login'] });
    });

    it('redirects to /select-group when authenticated without a group', () => {
      configure({ authed: true, hasGroup: false, isAdmin: false });
      expect(run(() => participantGuard(route, snapshot))).toEqual({ commands: ['/select-group'] });
    });

    it('allows an authenticated user with a group', () => {
      configure({ authed: true, hasGroup: true, isAdmin: false });
      expect(run(() => participantGuard(route, snapshot))).toBe(true);
    });
  });

  describe('adminGuard', () => {
    it('redirects to /login when not authenticated', () => {
      configure({ authed: false, hasGroup: true, isAdmin: true });
      expect(run(() => adminGuard(route, snapshot))).toEqual({ commands: ['/login'] });
    });

    it('redirects to /select-group when authenticated without a group', () => {
      configure({ authed: true, hasGroup: false, isAdmin: true });
      expect(run(() => adminGuard(route, snapshot))).toEqual({ commands: ['/select-group'] });
    });

    it('redirects a non-admin participant to /dashboard', () => {
      configure({ authed: true, hasGroup: true, isAdmin: false });
      expect(run(() => adminGuard(route, snapshot))).toEqual({ commands: ['/dashboard'] });
    });

    it('allows a group admin', () => {
      configure({ authed: true, hasGroup: true, isAdmin: true });
      expect(run(() => adminGuard(route, snapshot))).toBe(true);
    });
  });
});
