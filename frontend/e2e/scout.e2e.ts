import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const scout = {
  roundId: 'r8',
  roundNumber: 12,
  roundTitle: 'Primeira Rodada',
  matches: [
    {
      roundMatchId: 'm1',
      homeTeamName: 'Man United',
      awayTeamName: 'Man City',
      groups: [
        { homeScore: 1, awayScore: 1, names: ['Felipe'] },
        { homeScore: 2, awayScore: 0, names: ['Bruno', 'Dourado'] },
      ],
    },
    {
      roundMatchId: 'm2',
      homeTeamName: 'Arsenal',
      awayTeamName: 'Chelsea',
      groups: [{ homeScore: 3, awayScore: 1, names: ['Zé'] }],
    },
  ],
};

test.describe('Round scout', () => {
  test('admin sees the first match scout grouped by scoreline', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/admin/rounds/r8/scout'), respond: () => ({ json: scout }) },
    ]);

    await page.goto('/admin/rounds/r8/scout');

    await expect(page.getByText('Scout Man United x Man City')).toBeVisible();
    await expect(page.getByText('- 1x1 @Felipe')).toBeVisible();
    await expect(page.getByText('- 2x0 @Bruno @Dourado')).toBeVisible();
  });

  test('admin can switch to another match via the dropdown', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/admin/rounds/r8/scout'), respond: () => ({ json: scout }) },
    ]);

    await page.goto('/admin/rounds/r8/scout');

    await page.getByRole('combobox').selectOption({ label: 'Arsenal × Chelsea' });

    await expect(page.getByText('Scout Arsenal x Chelsea')).toBeVisible();
    await expect(page.getByText('- 3x1 @Zé')).toBeVisible();
    await expect(page.getByText('Scout Man United x Man City')).toHaveCount(0);
  });
});
