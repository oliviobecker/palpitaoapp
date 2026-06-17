import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const seasons = [
  {
    id: 's1',
    name: 'England 2025/2026',
    startDate: '2025-08-01',
    endDate: '2026-05-31',
    isActive: true,
  },
];

function candidate(
  externalId: string,
  home: string,
  away: string,
  startsAt: string,
  competition = 'PremierLeague',
  extra: Partial<Record<string, unknown>> = {},
) {
  return {
    externalId,
    competition,
    phase: 'Regular',
    homeTeamName: home,
    awayTeamName: away,
    startsAt,
    source: 'OneFootball',
    isBigSevenMatch: false,
    suggestedMultiplier: 1,
    isAlreadyAddedToRound: false,
    ...extra,
  };
}

test.describe('Round-by-period fixture import', () => {
  test('searches fixtures, selects matches and saves the round', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const searched: Array<Record<string, unknown>> = [];
    const imported: Array<Record<string, unknown>> = [];

    await installApi(page, [
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: seasons }) },
      {
        method: 'GET',
        match: path('/rounds'),
        respond: () => ({
          json: [
            { id: 'a', seasonId: 's1', number: 6, status: 'Scored' },
            { id: 'b', seasonId: 's1', number: 7, status: 'Scored' },
          ],
        }),
      },
      {
        method: 'POST',
        match: path('/admin/fixtures/search'),
        respond: (req) => {
          searched.push(req.postDataJSON());
          return {
            json: {
              source: 'OneFootball',
              fixtures: [
                candidate('of-1', 'Arsenal', 'Chelsea', '2026-08-15T13:30:00Z', 'PremierLeague', {
                  isBigSevenMatch: true,
                  suggestedMultiplier: 2,
                }),
                candidate('of-2', 'Luton', 'Reading', '2026-08-16T15:00:00Z', 'LeagueOne', {
                  suggestedMultiplier: 2,
                }),
              ],
            },
          };
        },
      },
      {
        method: 'POST',
        match: path('/rounds'),
        respond: () => ({ status: 201, json: { id: 'r1', number: 8, matches: [] } }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/rounds\/.+\/matches\/import$/.test(p),
        respond: (req) => {
          imported.push(req.postDataJSON());
          return {
            json: {
              importedCount: 2,
              skippedDuplicateCount: 0,
              createdTeamCount: 1,
              skippedDuplicates: [],
            },
          };
        },
      },
    ]);

    await page.goto('/admin/rounds/new');

    // Dates are pre-filled (today → +10) and a pre-search runs automatically,
    // so the fixtures show up without any interaction and the button is enabled.
    await expect(page.getByText('Arsenal')).toBeVisible();
    const searchBtn = page.getByRole('button', { name: 'Buscar jogos' });
    await expect(searchBtn).toBeEnabled();

    // Number and name are pre-filled sequentially (existing max is 7 → next 8).
    await expect(page.locator('input[formControlName="number"]')).toHaveValue('8');
    await expect(page.locator('input[formControlName="title"]')).toHaveValue('Oitava Rodada');

    await page.fill('input[formControlName="number"]', '8');
    await page.fill('input[formControlName="startDate"]', '2026-08-15');
    await page.fill('input[formControlName="endDate"]', '2026-08-17');

    await expect(searchBtn).toBeEnabled();
    await searchBtn.click();

    // Both fixtures render (an automatic pre-search already ran on load).
    await expect(page.getByText('Arsenal')).toBeVisible();
    await expect(page.getByText('Reading')).toBeVisible();
    expect(searched.length).toBeGreaterThanOrEqual(1);

    // Select all -> counter shows 2 selected.
    await page.getByRole('button', { name: 'Selecionar todos' }).click();
    await expect(
      page.locator('span.fw-semibold', { hasText: '2 jogo(s) selecionado(s)' }),
    ).toBeVisible();

    // Save the round -> create + import.
    await page.getByRole('button', { name: /Salvar rodada/ }).click();

    await expect(page.locator('.toast-body')).toContainText('2 jogo(s) importado(s)');
    expect(imported).toHaveLength(1);
    expect((imported[0].fixtures as unknown[]).length).toBe(2);
  });

  test('end date before start date blocks the search', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: seasons }) },
    ]);

    await page.goto('/admin/rounds/new');
    await page.fill('input[formControlName="startDate"]', '2026-08-17');
    await page.fill('input[formControlName="endDate"]', '2026-08-15');

    await expect(
      page.getByText('A data final deve ser maior ou igual à data inicial.'),
    ).toBeVisible();
    await expect(page.getByRole('button', { name: 'Buscar jogos' })).toBeDisabled();
  });

  test('imports fixtures into an existing round from the matches screen', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const existingRound = {
      id: 'r1',
      seasonId: 's1',
      number: 5,
      title: null,
      startDate: '2026-08-15T00:00:00Z',
      endDate: '2026-08-17T23:59:59Z',
      status: 'Draft',
      firstMatchStartsAt: null,
      publishedAt: null,
      lockedAt: null,
      mirrorPublishedAt: null,
      createdAt: '2026-01-01T00:00:00Z',
      matches: [],
    };
    const imported: Array<Record<string, unknown>> = [];

    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: existingRound }) },
      { method: 'GET', match: path('/teams'), respond: () => ({ json: [] }) },
      {
        method: 'POST',
        match: path('/admin/fixtures/search'),
        respond: () => ({
          json: {
            source: 'OneFootball',
            fixtures: [
              candidate('of-9', 'Arsenal', 'Chelsea', '2026-08-15T13:30:00Z', 'PremierLeague', {
                isBigSevenMatch: true,
                suggestedMultiplier: 2,
              }),
            ],
          },
        }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/rounds\/r1\/matches\/import$/.test(p),
        respond: (req) => {
          imported.push(req.postDataJSON());
          return {
            json: {
              importedCount: 1,
              skippedDuplicateCount: 0,
              createdTeamCount: 0,
              skippedDuplicates: [],
            },
          };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/matches');
    await expect(page.getByText('Importar jogos por período')).toBeVisible();

    // Dates are prefilled from the round window; search right away.
    await page.getByRole('button', { name: 'Buscar jogos' }).click();
    await expect(page.getByText('Arsenal')).toBeVisible();

    await page.getByRole('button', { name: 'Selecionar todos' }).click();
    await page.getByRole('button', { name: /Adicionar jogos selecionados/ }).click();

    await expect(page.locator('.toast-body')).toContainText('1 jogo(s) importado(s)');
    expect(imported).toHaveLength(1);
    expect((imported[0].fixtures as unknown[]).length).toBe(1);
  });

  test('auto pre-searches fixtures on load when the round has no period', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const roundNoPeriod = {
      id: 'r2',
      seasonId: 's1',
      number: 6,
      title: null,
      startDate: null,
      endDate: null,
      status: 'Draft',
      firstMatchStartsAt: null,
      publishedAt: null,
      lockedAt: null,
      mirrorPublishedAt: null,
      createdAt: '2026-01-01T00:00:00Z',
      matches: [],
    };
    const searchCalls: Array<Record<string, unknown>> = [];

    await installApi(page, [
      { method: 'GET', match: path('/rounds/r2'), respond: () => ({ json: roundNoPeriod }) },
      { method: 'GET', match: path('/teams'), respond: () => ({ json: [] }) },
      {
        method: 'POST',
        match: path('/admin/fixtures/search'),
        respond: (req) => {
          searchCalls.push(req.postDataJSON());
          return {
            json: {
              source: 'TheSportsDB',
              fixtures: [
                candidate('of-auto', 'Arsenal', 'Chelsea', '2026-08-15T13:30:00Z', 'PremierLeague'),
              ],
            },
          };
        },
      },
    ]);

    await page.goto('/admin/rounds/r2/matches');

    // No click on "Buscar jogos": the list is pre-populated automatically.
    await expect(page.getByText('Arsenal')).toBeVisible();
    await expect(
      page.locator('span.fw-semibold', { hasText: '0 jogo(s) selecionado(s)' }),
    ).toBeVisible();
    expect(searchCalls).toHaveLength(1);
  });
});
