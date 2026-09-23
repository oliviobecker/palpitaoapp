import { expect, test } from '@playwright/test';
import { installApi, path, round, seedAuth } from './support';

/**
 * A week with two lists is one round played in parts ("7.1" + "7.2"): it counts as a single
 * round for absences, decided by its last part. Grouping renumbers the rounds after it and,
 * when a scored round is involved, replays the season — so every dialog spells that out.
 */

const standaloneMove = { allowed: false, renumberedRounds: 0, requiresRecalculation: false };

const round8 = {
  ...round,
  id: 'r8',
  number: 8,
  part: 0,
  week: {
    parts: [{ id: 'r8', number: 8, part: 0, status: 'Published' }],
    decidesAbsences: true,
    openPartBlocksFinalize: false,
    laterPartScored: false,
    joinPrevious: {
      allowed: true,
      targetNumber: 7,
      targetPart: 2,
      renumberedRounds: 2,
      requiresRecalculation: true,
    },
    leave: standaloneMove,
  },
};

const part72 = {
  ...round8,
  number: 7,
  part: 2,
  week: {
    parts: [
      { id: 'r7', number: 7, part: 1, status: 'Scored' },
      { id: 'r8', number: 7, part: 2, status: 'Published' },
    ],
    decidesAbsences: true,
    openPartBlocksFinalize: false,
    laterPartScored: false,
    joinPrevious: standaloneMove,
    leave: {
      allowed: true,
      targetNumber: 8,
      targetPart: 0,
      renumberedRounds: 2,
      requiresRecalculation: true,
    },
  },
};

/** Everyone predicted: keeps the coverage panel quiet on a Published/Locked round. */
const coverage = {
  method: 'GET',
  match: path('/admin/rounds/r8/predictions/coverage'),
  respond: () => ({
    json: {
      roundId: 'r8',
      matchCount: 2,
      totalParticipants: 0,
      completeParticipants: 0,
      missing: [],
    },
  }),
};

test.describe('Rounds played in parts', () => {
  test('groups a round with the previous one from its detail', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let joined = false;

    await installApi(page, [
      coverage,
      {
        method: 'GET',
        match: path('/rounds/r8'),
        respond: () => ({ json: joined ? part72 : round8 }),
      },
      {
        method: 'POST',
        match: path('/rounds/r8/join-previous-week'),
        respond: () => {
          joined = true;
          return { json: part72 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r8');

    const join = page.getByRole('button', { name: /Agrupar com a rodada anterior/ });
    await expect(join).toContainText('vira a Rodada 7.2');
    await join.click();

    const modal = page.locator('.modal');
    await expect(modal).toContainText('A Rodada 8 vai virar a Rodada 7.2');
    await expect(modal).toContainText('2 rodadas mudam de número ou de parte');
    await expect(modal).toContainText('a temporada será recalculada');
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Rodada agrupada.');
    expect(joined).toBe(true);
    await expect(page.locator('h2', { hasText: 'Rodada 7.2' })).toBeVisible();
    await expect(page.getByText('Rodada 7 em partes:')).toBeVisible();
  });

  test('a part keeps its number, explains who decides and can leave', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let left = false;

    await installApi(page, [
      coverage,
      { method: 'GET', match: path('/rounds/r8'), respond: () => ({ json: part72 }) },
      {
        method: 'POST',
        match: path('/rounds/r8/leave-week'),
        respond: () => {
          left = true;
          return { json: round8 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r8');

    await expect(page.locator('#rd-number')).toBeDisabled();
    await expect(page.getByText('O número desta parte acompanha a rodada agrupada')).toBeVisible();
    await expect(page.getByText('Esta parte decide as ausências da rodada')).toBeVisible();
    await expect(page.locator('a.badge', { hasText: '7.1' })).toBeVisible();

    const leave = page.getByRole('button', { name: /Desagrupar/ });
    await expect(leave).toContainText('Rodada 8');
    await leave.click();
    const modal = page.locator('.modal');
    await expect(modal).toContainText(
      'A Rodada 7.2 vai sair da rodada agrupada e virar a Rodada 8',
    );
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Rodada desagrupada.');
    expect(left).toBe(true);
  });

  test('the last part waits until the other parts stop taking predictions', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const lockedLastPart = {
      ...part72,
      status: 'Locked',
      lockedAt: '2026-01-02T00:00:00Z',
      matches: round.matches.map((m) => ({
        ...m,
        homeScore: 1,
        awayScore: 0,
        isFinished: true,
        status: 'Finished',
      })),
      week: {
        ...part72.week,
        parts: [
          { id: 'r7', number: 7, part: 1, status: 'Published' },
          { id: 'r8', number: 7, part: 2, status: 'Locked' },
        ],
        openPartBlocksFinalize: true,
      },
    };

    await installApi(page, [
      coverage,
      { method: 'GET', match: path('/rounds/r8'), respond: () => ({ json: lockedLastPart }) },
    ]);

    await page.goto('/admin/rounds/r8');

    await expect(page.getByText('Bloqueie ou cancele as outras partes desta rodada')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Finalizar rodada' })).toBeDisabled();
  });

  test('creates the second list of a week as a part of the previous round', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const created: Array<Record<string, unknown>> = [];

    await installApi(page, [
      {
        method: 'GET',
        match: path('/seasons'),
        respond: () => ({
          json: [
            {
              id: 's1',
              name: 'England',
              startDate: '2025-08-01',
              endDate: '2026-05-31',
              isActive: true,
            },
          ],
        }),
      },
      {
        method: 'GET',
        match: path('/rounds'),
        respond: () => ({
          json: [{ id: 'r7', seasonId: 's1', number: 7, part: 0, status: 'Scored' }],
        }),
      },
      {
        method: 'POST',
        match: path('/admin/fixtures/search'),
        respond: () => ({ json: { source: 'OneFootball', fixtures: [] } }),
      },
      {
        method: 'POST',
        match: path('/rounds'),
        respond: (req) => {
          created.push(req.postDataJSON());
          return { status: 201, json: { id: 'new1', number: 7, part: 2, matches: [] } };
        },
      },
    ]);

    await page.goto('/admin/rounds/new');
    await expect(page.locator('input[formControlName="number"]')).toHaveValue('8');

    await page.getByLabel('Mesma semana da rodada 7 (rodada em partes)').check();

    await expect(page.getByText('Será criada como Rodada 7.2')).toBeVisible();
    await expect(page.getByText('A rodada 7 já foi pontuada')).toBeVisible();
    // A part takes the name of the round it joins.
    await expect(page.locator('input[formControlName="title"]')).toHaveValue('Sétima Rodada');

    await page.getByRole('button', { name: 'Criar rodada e adicionar jogos manualmente' }).click();

    await expect(page).toHaveURL(/\/admin\/rounds\/new1\/matches$/);
    expect(created).toHaveLength(1);
    expect(created[0]).toMatchObject({ seasonId: 's1', number: 8, joinPreviousWeek: true });
  });
});
