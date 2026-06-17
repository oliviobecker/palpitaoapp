import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const roundWithMatches = {
  id: 'r7',
  seasonId: 's1',
  number: 41,
  title: null,
  startDate: '2026-05-20T00:00:00Z',
  endDate: '2026-05-24T23:59:59Z',
  status: 'Published',
  firstMatchStartsAt: '2026-05-23T13:30:00Z',
  publishedAt: '2026-05-20T10:00:00Z',
  lockedAt: null,
  mirrorPublishedAt: null,
  flavio: {
    applies: true,
    leaderNames: ['Manoel Neto'],
    deadlineUtc: '2026-05-22T23:59:00Z',
  },
  createdAt: '2026-01-01T00:00:00Z',
  matches: [
    {
      id: 'm1',
      roundId: 'r7',
      competition: 'PremierLeague',
      phase: 'Regular',
      homeTeamId: 't1',
      homeTeamName: 'Arsenal',
      awayTeamId: 't2',
      awayTeamName: 'Chelsea',
      startsAt: '2026-05-23T13:30:00Z',
      order: 0,
      isFinished: false,
    },
    {
      id: 'm2',
      roundId: 'r7',
      competition: 'LeagueOne',
      phase: 'Regular',
      homeTeamId: 't3',
      homeTeamName: 'Bolton',
      awayTeamId: 't4',
      awayTeamName: 'Stockport',
      startsAt: '2026-05-24T12:00:00Z',
      order: 1,
      isFinished: false,
    },
  ],
};

test.describe('Round group message', () => {
  test('shows the copy-ready message and copies it', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r7'), respond: () => ({ json: roundWithMatches }) },
    ]);

    await page.goto('/admin/rounds/r7');

    await expect(page.getByText('Mensagem para o grupo')).toBeVisible();
    const pre = page.locator('pre');
    await expect(pre).toContainText('Palpitão England 2025/2026');
    await expect(pre).toContainText('Rodada 41');
    await expect(pre).toContainText('Arsenal x Chelsea (×2)');
    await expect(pre).toContainText('Bolton x Stockport (×2)');
    await expect(pre).toContainText('Palpites até');
    await expect(pre).toContainText('Líder @Manoel Neto tem até');

    await page.getByRole('button', { name: /Copiar/ }).click();
    await expect(page.locator('.toast-body')).toHaveText('Mensagem copiada!');

    const clip = await page.evaluate(() => navigator.clipboard.readText());
    expect(clip).toContain('Rodada 41');
    expect(clip).toContain('Arsenal x Chelsea (×2)');
  });
});
