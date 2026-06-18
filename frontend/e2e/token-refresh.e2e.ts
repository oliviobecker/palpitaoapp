import { expect, test } from '@playwright/test';
import { adminUser, installApi, path, seedAuth } from './support';

const refreshResponse = {
  token: 'refreshed-jwt',
  expiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'rotated-refresh',
  refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  user: adminUser,
};

const publishedRound = {
  id: 'r1',
  seasonId: 's1',
  number: 5,
  title: null,
  status: 'Published',
  firstMatchStartsAt: '2999-01-01T18:00:00Z',
  matchCount: 2,
  allowParticipantsToViewOthersPredictions: false,
  allowParticipantsToSubmitPredictions: true,
};

test.describe('Access token refresh', () => {
  test('a 401 transparently refreshes the token and retries the request', async ({ page }) => {
    await seedAuth(page, 'en-US');

    let roundsCalls = 0;
    let refreshCalls = 0;
    await installApi(page, [
      {
        method: 'POST',
        match: path('/auth/refresh'),
        respond: () => {
          refreshCalls++;
          return { json: refreshResponse };
        },
      },
      {
        method: 'GET',
        match: path('/rounds'),
        respond: () => {
          roundsCalls++;
          // First call: the access token is "expired". After a refresh, it succeeds.
          return roundsCalls === 1
            ? { status: 401, json: { message: 'expired' } }
            : { json: [publishedRound] };
        },
      },
    ]);

    await page.goto('/rounds');

    // The retried request rendered the round, so refresh + retry worked end-to-end.
    await expect(page.getByText('Round 5')).toBeVisible();
    // The refresh ran exactly once and we stayed authenticated (no redirect to login).
    expect(refreshCalls).toBe(1);
    await expect(page).toHaveURL(/\/rounds$/);
    // The rotated refresh token replaced the old one in storage.
    const stored = await page.evaluate(() => localStorage.getItem('palpitao.refreshToken'));
    expect(stored).toBe('rotated-refresh');
  });

  test('a failed refresh ends the session and redirects to login', async ({ page }) => {
    await seedAuth(page, 'en-US');

    await installApi(page, [
      { method: 'POST', match: path('/auth/refresh'), respond: () => ({ status: 401, json: {} }) },
      {
        method: 'GET',
        match: path('/rounds'),
        respond: () => ({ status: 401, json: { message: 'expired' } }),
      },
    ]);

    await page.goto('/rounds');

    await expect(page).toHaveURL(/\/login$/);
    const token = await page.evaluate(() => localStorage.getItem('palpitao.token'));
    expect(token).toBeNull();
  });
});
