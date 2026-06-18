import { expect, test } from '@playwright/test';
import { installApi } from './support';

/**
 * Product branding is per-language: FanPicks (en-US), Palpitão (pt-BR). Group
 * names (e.g. "England 2025/2026") are separate and never the product name.
 */
test.describe('Product branding', () => {
  test('login shows FanPicks in English', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'en-US'));
    await installApi(page, []);

    await page.goto('/login');

    await expect(page.getByRole('heading', { name: 'FanPicks' })).toBeVisible();
    await expect(page).toHaveTitle('FanPicks');
  });

  test('login shows Palpitão in Portuguese', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));
    await installApi(page, []);

    await page.goto('/login');

    await expect(page.getByRole('heading', { name: 'Palpitão' })).toBeVisible();
    await expect(page).toHaveTitle('Palpitão');
  });
});
