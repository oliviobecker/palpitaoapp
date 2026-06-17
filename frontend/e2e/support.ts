import { Page, Request } from '@playwright/test';

/** Backend host the dev build talks to (environment.development.ts). */
export const API = 'https://localhost:7099';

export const adminUser = {
  id: 'admin-1',
  name: 'Admin',
  email: 'admin@palpitao.local',
  role: 'Admin',
  isActive: true,
};

export const round = {
  id: 'r1',
  seasonId: 's1',
  number: 5,
  title: null,
  status: 'Published',
  firstMatchStartsAt: '2999-01-01T18:00:00Z',
  publishedAt: '2026-01-01T00:00:00Z',
  lockedAt: null,
  mirrorPublishedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  allowParticipantsToSubmitPredictions: true,
  allowParticipantsToViewOthersPredictions: false,
  matches: [
    {
      id: 'm1',
      roundId: 'r1',
      competition: 'PremierLeague',
      phase: 'Regular',
      homeTeamId: 't1',
      homeTeamName: 'Arsenal',
      awayTeamId: 't2',
      awayTeamName: 'Chelsea',
      startsAt: '2999-01-01T18:00:00Z',
      order: 0,
      isFinished: false,
    },
    {
      id: 'm2',
      roundId: 'r1',
      competition: 'PremierLeague',
      phase: 'Regular',
      homeTeamId: 't3',
      homeTeamName: 'Liverpool',
      awayTeamId: 't4',
      awayTeamName: 'Newcastle',
      startsAt: '2999-01-01T20:00:00Z',
      order: 1,
      isFinished: false,
    },
  ],
};

export const participants = [
  {
    id: 'p1',
    name: 'João Silva',
    email: 'joao@x.com',
    isActive: true,
    isEliminated: false,
    totalPoints: 0,
    absenceCount: 0,
    penaltyPoints: 0,
  },
  {
    id: 'p2',
    name: 'Maria Souza',
    email: 'maria@x.com',
    isActive: true,
    isEliminated: false,
    totalPoints: 0,
    absenceCount: 0,
    penaltyPoints: 0,
  },
];

/** A 1x1 transparent PNG, enough to satisfy the file input + preview. */
export const pngBytes = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

/** The group context seeded by seedAuth (the user acts as a GroupAdmin of it). */
export const currentGroup = {
  groupId: 'g1',
  groupName: 'Palpitão England 2025/2026',
  slug: 'palpitao-england-2025-2026',
  role: 'GroupAdmin',
  status: 'Approved',
};

/**
 * Seeds an authenticated admin + a selected group (and optional language) into
 * localStorage before load, so the group-aware guards let the page render.
 */
export async function seedAuth(page: Page, lang?: 'pt-BR' | 'en-US'): Promise<void> {
  await page.addInitScript(
    ({ user, group, lng }) => {
      localStorage.setItem('palpitao.token', 'e2e-fake-jwt');
      localStorage.setItem('palpitao.user', JSON.stringify(user));
      localStorage.setItem('palpitao.groupId', group.groupId);
      localStorage.setItem('palpitao.groupName', group.groupName);
      localStorage.setItem('palpitao.groupRole', group.role);
      if (lng) {
        localStorage.setItem('palpitao.lang', lng);
      }
    },
    { user: adminUser, group: currentGroup, lng: lang ?? null },
  );
}

const CORS = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'GET,POST,PUT,DELETE,OPTIONS',
  'access-control-allow-headers': 'authorization,content-type,accept-language,x-group-id',
};

export interface ApiHandler {
  method: string;
  match: (path: string) => boolean;
  respond: (
    req: Request,
  ) => { status?: number; json?: unknown } | Promise<{ status?: number; json?: unknown }>;
}

/**
 * Intercepts every call to the backend host and answers from the provided
 * handlers. Adds CORS headers (cross-origin dev) and handles preflight OPTIONS.
 * Unmatched calls get an empty 200 so the UI never hangs.
 */
export async function installApi(page: Page, handlers: ApiHandler[]): Promise<void> {
  await page.route(`${API}/**`, async (route) => {
    const req = route.request();
    if (req.method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS });
      return;
    }

    const path = new URL(req.url()).pathname;
    const handler = handlers.find((h) => h.method === req.method() && h.match(path));
    if (!handler) {
      await route.fulfill({
        status: 200,
        headers: { ...CORS, 'content-type': 'application/json' },
        body: '{}',
      });
      return;
    }

    const result = await handler.respond(req);
    const status = result.status ?? 200;
    if (status === 204) {
      await route.fulfill({ status, headers: CORS });
      return;
    }

    await route.fulfill({
      status,
      headers: { ...CORS, 'content-type': 'application/json' },
      body: JSON.stringify(result.json ?? {}),
    });
  });
}

export const path = (p: string) => (actual: string) => actual === p;
