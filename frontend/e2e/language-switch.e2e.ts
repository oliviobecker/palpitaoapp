import { expect, test } from '@playwright/test';
import { installApi, participants, path, round, seedAuth } from './support';

test.describe('Language switching', () => {
  test('switches UI language, persists it, and sends Accept-Language to the API', async ({
    page,
  }) => {
    // Start in English deterministically (default detection is covered below).
    await seedAuth(page, 'en-US');

    const acceptLanguages: string[] = [];
    await installApi(page, [
      {
        method: 'GET',
        match: path('/rounds/r1'),
        respond: (req) => {
          acceptLanguages.push(req.headers()['accept-language'] ?? '');
          return { json: round };
        },
      },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: (p) => p.startsWith('/admin/rounds/r1/predictions/participant/'),
        respond: () => ({
          json: { roundId: 'r1', userId: 'p1', hasPredictions: false, predictions: [] },
        }),
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');

    // Default English.
    await expect(page.getByText('Register predictions')).toBeVisible();

    // Switch to Portuguese.
    await page.getByRole('button', { name: 'PT', exact: true }).click();
    await expect(page.getByText('Registrar palpites')).toBeVisible();
    await expect(page.getByText('Register predictions')).toHaveCount(0);

    // Switch back to English.
    await page.getByRole('button', { name: 'EN', exact: true }).click();
    await expect(page.getByText('Register predictions')).toBeVisible();

    // Persisted across reload.
    await page.reload();
    await expect(page.getByText('Register predictions')).toBeVisible();
    expect(await page.evaluate(() => localStorage.getItem('palpitao.lang'))).toBe('en-US');

    // The API received the selected language.
    expect(acceptLanguages.length).toBeGreaterThan(0);
    expect(acceptLanguages.at(-1)?.toLowerCase()).toContain('en');
  });

  test('honours a previously saved Portuguese preference on load', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await expect(page.getByText('Registrar palpites')).toBeVisible();
    await expect(page.locator('button.is-active', { hasText: 'PT' })).toBeVisible();
  });
});
