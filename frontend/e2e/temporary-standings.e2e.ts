import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const publishedRound = {
  id: 'r8',
  seasonId: 's1',
  number: 12,
  title: null,
  startDate: null,
  endDate: null,
  status: 'Published',
  firstMatchStartsAt: '2026-05-23T13:30:00Z',
  publishedAt: '2026-05-20T10:00:00Z',
  lockedAt: null,
  mirrorPublishedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  matches: [
    {
      id: 'm1',
      roundId: 'r8',
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
  ],
};

test.describe('Results refresh + temporary standings', () => {
  test('admin refreshes results and sees the summary', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const refreshCalls: string[] = [];

    await installApi(page, [
      { method: 'GET', match: path('/rounds/r8'), respond: () => ({ json: publishedRound }) },
      {
        method: 'POST',
        match: (p) => /\/admin\/rounds\/r8\/refresh-results$/.test(p),
        respond: (req) => {
          refreshCalls.push(req.url());
          return {
            json: {
              message: 'Resultados atualizados com sucesso.',
              roundId: 'r8',
              provider: 'Manual',
              providerEnabled: false,
              updatedMatches: 0,
              finishedMatches: 1,
              inProgressMatches: 1,
              notStartedMatches: 0,
              postponedMatches: 0,
              cancelledMatches: 0,
              temporaryStandingsUpdatedAt: '2026-05-22T20:30:00Z',
            },
          };
        },
      },
    ]);

    await page.goto('/admin/rounds/r8');

    await page.getByRole('button', { name: /Atualizar resultados/ }).click();

    await expect(page.locator('.toast-body')).toHaveText('Resultados atualizados com sucesso.');
    expect(refreshCalls).toHaveLength(1);
    // Summary card shows the counts.
    await expect(page.getByText(/Finalizados:/)).toBeVisible();
  });

  test('participant sees the temporary standings with the warning banner', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      {
        method: 'GET',
        match: path('/rounds/r8/temporary-standings'),
        respond: () => ({
          json: {
            roundId: 'r8',
            isTemporary: true,
            roundStatus: 'Published',
            lastUpdatedAt: '2026-05-22T20:30:00Z',
            computedMatches: 1,
            remainingMatches: 0,
            standings: [
              {
                position: 1,
                userId: 'u1',
                name: 'João Silva',
                roundTemporaryPoints: 18,
                currentOfficialTotalPoints: 120,
                projectedTotalPoints: 138,
                computedMatches: 1,
                remainingMatches: 0,
              },
            ],
          },
        }),
      },
    ]);

    await page.goto('/rounds/r8/temporary-standings');

    await expect(
      page.getByText('Classificação temporária — os pontos podem mudar até o fim da rodada.'),
    ).toBeVisible();
    await expect(page.getByText('João Silva')).toBeVisible();
    await expect(page.getByText('+18')).toBeVisible();
    await expect(page.getByText('Oficial: 120 · Projetada: 138')).toBeVisible();
  });
});
