import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const scout = {
  roundId: 'r8',
  roundNumber: 12,
  roundTitle: 'Primeira Rodada',
  matches: [
    {
      roundMatchId: 'm1',
      startsAt: '2026-08-22T14:00:00Z',
      homeTeamName: 'Man United',
      awayTeamName: 'Man City',
      groups: [
        { homeScore: 1, awayScore: 1, names: ['Felipe'] },
        { homeScore: 2, awayScore: 0, names: ['Bruno', 'Dourado'] },
      ],
    },
    {
      roundMatchId: 'm2',
      startsAt: '2026-08-22T16:30:00Z',
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
    await expect(page.getByRole('combobox')).toContainText(
      /\d{2}\/\d{2}\s+\d{2}:\d{2}\s+·\s+Arsenal\s+×\s+Chelsea/,
    );
    await expect(page.getByText('- 1x1 @Felipe')).toBeVisible();
    await expect(page.getByText('- 2x0 @Bruno @Dourado')).toBeVisible();
  });

  test('admin can switch to another match via the dropdown', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/admin/rounds/r8/scout'), respond: () => ({ json: scout }) },
    ]);

    await page.goto('/admin/rounds/r8/scout');

    // By value: the option label now carries the kickoff, which depends on the timezone.
    await page.getByRole('combobox').selectOption('m2');

    await expect(page.getByText('Scout Arsenal x Chelsea')).toBeVisible();
    await expect(page.getByText('- 3x1 @Zé')).toBeVisible();
    await expect(page.getByText('Scout Man United x Man City')).toHaveCount(0);
  });
});
