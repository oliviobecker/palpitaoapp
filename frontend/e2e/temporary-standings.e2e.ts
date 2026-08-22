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
      // Being played right now: the round detail must say so, not treat it as upcoming.
      status: 'InProgress',
      homeScore: 3,
      awayScore: 0,
    },
  ],
};

// Ranked by the points earned in *this* round, so the runner-up sits on the bigger
// projected total — the group message must keep this order, not the projected one.
const temporaryStandings = {
  roundId: 'r8',
  roundNumber: 12,
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
    {
      position: 2,
      userId: 'u2',
      name: 'Maria Souza',
      roundTemporaryPoints: 5,
      currentOfficialTotalPoints: 300,
      projectedTotalPoints: 305,
      computedMatches: 1,
      remainingMatches: 0,
    },
  ],
};

const temporaryStandingsRoute = {
  method: 'GET' as const,
  match: path('/rounds/r8/temporary-standings'),
  respond: () => ({ json: temporaryStandings }),
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
              unmatchedMatches: 0,
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
    // …and the match itself is marked as being played, with the score so far.
    await expect(page.getByText('Ao vivo')).toBeVisible();
    await expect(page.getByText('3 - 0')).toBeVisible();
  });

  test('participant sees the temporary standings with the warning banner', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [temporaryStandingsRoute]);

    await page.goto('/rounds/r8/temporary-standings');

    await expect(
      page.getByText('Classificação temporária — os pontos podem mudar até o fim da rodada.'),
    ).toBeVisible();
    // Scoped to the list: the copy-ready message below repeats the same names and points.
    const list = page.locator('.vstack');
    await expect(list.getByText('João Silva')).toBeVisible();
    await expect(list.getByText('+18')).toBeVisible();
    await expect(list.getByText('Oficial: 120 · Projetada: 138')).toBeVisible();
  });

  test('participant copies the temporary standings as a group message', async ({
    page,
    context,
  }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await seedAuth(page, 'pt-BR');
    await installApi(page, [temporaryStandingsRoute]);

    await page.goto('/rounds/r8/temporary-standings');

    const pre = page.locator('pre');
    await expect(pre).toContainText('*Palpitão England 2025/2026*');
    await expect(pre).toContainText('Rodada 12 — parcial');
    await expect(pre).toContainText('1. João Silva: +18 (138)');
    await expect(pre).toContainText('2. Maria Souza: +5 (305)');
    await expect(pre).toContainText('(x) = total projetado no geral');
    await expect(pre).toContainText('1 jogo computado · 0 restantes');

    await page.getByRole('button', { name: /Copiar/ }).click();
    await expect(page.locator('.toast-body')).toHaveText('Mensagem copiada!');

    const clip = await page.evaluate(() => navigator.clipboard.readText());
    expect(clip).toContain('Rodada 12 — parcial');
    // The message keeps the round ranking, not the projected-total one.
    expect(clip).toMatch(/1\. João Silva: \+18 \(138\)[\s\S]*2\. Maria Souza: \+5 \(305\)/);
  });
});
