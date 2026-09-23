import { expect, Page, test } from '@playwright/test';
import { installApi, participants, path, round, seedAuth } from './support';

/** Types the score boxes in screen order: m1 home, m1 away, m2 home, m2 away. */
async function fillScores(page: Page, values: string[]): Promise<void> {
  const boxes = page.locator('input[type="number"]');
  for (const [i, value] of values.entries()) {
    await boxes.nth(i).fill(value);
  }
}

test.describe('Admin manual predictions', () => {
  test('preloads a participant existing predictions and saves an overwrite', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const saved: Array<Record<string, unknown>> = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/participant/p1'),
        respond: () => ({
          json: {
            roundId: 'r1',
            userId: 'p1',
            hasPredictions: true,
            predictions: [
              {
                roundMatchId: 'm1',
                predictedHomeScore: 2,
                predictedAwayScore: 1,
                source: 'AdminManual',
                updatedAt: null,
              },
              {
                roundMatchId: 'm2',
                predictedHomeScore: 0,
                predictedAwayScore: 3,
                source: 'AdminManual',
                updatedAt: null,
              },
            ],
          },
        }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/manual'),
        respond: (req) => {
          saved.push(req.postDataJSON());
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await expect(page.getByText('Registrar palpites')).toBeVisible();

    // Selecting the participant preloads their current scores.
    await page.locator('select').selectOption('p1');

    const scores = page.locator('input[type="number"]');
    await expect(scores).toHaveCount(4);
    await expect(scores.nth(0)).toHaveValue('2'); // m1 home
    await expect(scores.nth(1)).toHaveValue('1'); // m1 away
    await expect(scores.nth(2)).toHaveValue('0'); // m2 home
    await expect(scores.nth(3)).toHaveValue('3'); // m2 away

    // Overwrite is auto-armed with an explicit warning.
    await expect(
      page.getByText('Palpites carregados — salvar irá sobrescrever os existentes.'),
    ).toBeVisible();
    await expect(page.locator('#ow')).toBeChecked();
    // A complete set has no gaps to point at.
    await expect(page.getByText('sem palpite — preencha antes de salvar')).toHaveCount(0);

    // Change one score and save.
    await scores.nth(0).fill('4');
    await page.getByRole('button', { name: 'Salvar palpites' }).click();

    await expect(page.locator('.toast-body')).toHaveText('Palpites salvos!');

    expect(saved).toHaveLength(1);
    expect(saved[0].userId).toBe('p1');
    expect(saved[0].overwriteExisting).toBe(true);
    const predictions = saved[0].predictions as Array<Record<string, number>>;
    expect(predictions.find((p) => p.roundMatchId === 'm1')).toMatchObject({
      predictedHomeScore: 4,
      predictedAwayScore: 1,
    });
    expect(predictions.find((p) => p.roundMatchId === 'm2')).toMatchObject({
      predictedHomeScore: 0,
      predictedAwayScore: 3,
    });
  });

  test('a partial set leaves the missing match empty and blocks saving until it is typed', async ({
    page,
  }) => {
    // Production, 23/09/2026: the OCR import missed a line, the screen showed that match as 0x0
    // and saving stored a 0x0 nobody typed. The gap must stay empty and stop the save.
    await seedAuth(page, 'pt-BR');
    const saved: Array<Record<string, unknown>> = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/participant/p1'),
        respond: () => ({
          json: {
            roundId: 'r1',
            userId: 'p1',
            hasPredictions: true,
            predictions: [
              {
                roundMatchId: 'm1',
                predictedHomeScore: 2,
                predictedAwayScore: 1,
                source: 'AdminOcr',
                updatedAt: null,
              },
            ],
          },
        }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/manual'),
        respond: (req) => {
          saved.push(req.postDataJSON());
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await page.locator('select').selectOption('p1');

    const scores = page.locator('input[type="number"]');
    await expect(scores.nth(0)).toHaveValue('2'); // m1 home
    await expect(scores.nth(1)).toHaveValue('1'); // m1 away
    // m2 has no prediction: empty and flagged, not a 0x0.
    await expect(scores.nth(2)).toHaveValue('');
    await expect(scores.nth(3)).toHaveValue('');
    await expect(scores.nth(2)).toHaveAttribute('placeholder', '–');
    await expect(scores.nth(2)).toHaveClass(/is-invalid/);
    await expect(scores.nth(3)).toHaveClass(/is-invalid/);
    await expect(scores.nth(0)).not.toHaveClass(/is-invalid/);
    await expect(page.getByText('1 jogo(s) sem palpite — preencha antes de salvar.')).toBeVisible();

    // Saving with the gap is refused on the screen: nothing reaches the API.
    await page.getByRole('button', { name: 'Salvar palpites' }).click();
    await expect(page.getByText('Preencha todos os placares — faltam 1.')).toBeVisible();
    expect(saved).toHaveLength(0);

    // Typing the missing score clears the message and the save carries every match.
    await scores.nth(2).fill('3');
    await scores.nth(3).fill('1');
    await expect(page.getByText('Preencha todos os placares — faltam 1.')).toHaveCount(0);
    await page.getByRole('button', { name: 'Salvar palpites' }).click();

    await expect(page.locator('.toast-body')).toHaveText('Palpites salvos!');
    expect(saved).toHaveLength(1);
    expect(saved[0].predictions).toEqual([
      { roundMatchId: 'm1', predictedHomeScore: 2, predictedAwayScore: 1 },
      { roundMatchId: 'm2', predictedHomeScore: 3, predictedAwayScore: 1 },
    ]);
  });

  test('a participant with no predictions starts empty and cannot be saved blank', async ({
    page,
  }) => {
    await seedAuth(page, 'pt-BR');
    const saved: Array<Record<string, unknown>> = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/participant/p2'),
        respond: () => ({
          json: { roundId: 'r1', userId: 'p2', hasPredictions: false, predictions: [] },
        }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/manual'),
        respond: (req) => {
          saved.push(req.postDataJSON());
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await page.locator('select').selectOption('p2');

    await expect(page.locator('#ow')).not.toBeChecked();
    await expect(
      page.getByText('Palpites carregados — salvar irá sobrescrever os existentes.'),
    ).toHaveCount(0);
    const scores = page.locator('input[type="number"]');
    await expect(scores).toHaveCount(4);
    for (let i = 0; i < 4; i++) {
      await expect(scores.nth(i)).toHaveValue('');
      // Not flagged before anyone tried to save.
      await expect(scores.nth(i)).not.toHaveClass(/is-invalid/);
    }

    await page.getByRole('button', { name: 'Salvar palpites' }).click();
    await expect(page.getByText('Preencha todos os placares — faltam 2.')).toBeVisible();
    for (let i = 0; i < 4; i++) {
      await expect(scores.nth(i)).toHaveClass(/is-invalid/);
    }
    expect(saved).toHaveLength(0);
  });

  test('registers after the deadline with no override while the round is not finalized', async ({
    page,
  }) => {
    // The board often gets the WhatsApp screenshots in late: past the deadline, on a round
    // nobody has finalized yet, saving must just work — no "overwrite" tick, no justification.
    await seedAuth(page, 'pt-BR');
    const lateRound = {
      ...round,
      status: 'Locked',
      firstMatchStartsAt: '2026-01-01T18:00:00Z',
      matches: round.matches.map((m) => ({ ...m, startsAt: '2026-01-01T18:00:00Z' })),
    };
    const saved: Array<Record<string, unknown>> = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: lateRound }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/participant/p2'),
        respond: () => ({
          json: { roundId: 'r1', userId: 'p2', hasPredictions: false, predictions: [] },
        }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/manual'),
        respond: (req) => {
          saved.push(req.postDataJSON());
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await page.locator('select').selectOption('p2');

    await expect(page.getByRole('alert')).toHaveCount(0);
    await expect(page.getByPlaceholder('Justificativa')).toHaveCount(0);
    await fillScores(page, ['1', '0', '2', '2']);
    await page.getByRole('button', { name: 'Salvar palpites' }).click();

    await expect(page.locator('.toast-body')).toHaveText('Palpites salvos!');
    expect(saved).toHaveLength(1);
    expect(saved[0]).toMatchObject({ overwriteExisting: false, allowAfterDeadline: false });
  });

  test('a finalized round points to reopen it and cannot be saved', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      {
        method: 'GET',
        match: path('/rounds/r1'),
        respond: () => ({ json: { ...round, status: 'Scored' } }),
      },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/participant/p2'),
        respond: () => ({
          json: { roundId: 'r1', userId: 'p2', hasPredictions: false, predictions: [] },
        }),
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await page.locator('select').selectOption('p2');

    await expect(page.getByRole('alert')).toContainText('Esta rodada já foi finalizada.');
    await expect(page.getByRole('link', { name: 'Abrir a rodada' })).toHaveAttribute(
      'href',
      '/admin/rounds/r1',
    );
    await expect(page.getByRole('button', { name: 'Salvar palpites' })).toBeDisabled();
  });

  test('an eliminated participant still needs the justified override', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const withEliminated = participants.map((p) =>
      p.id === 'p2' ? { ...p, isEliminated: true } : p,
    );
    const saved: Array<Record<string, unknown>> = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: withEliminated }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/participant/p2'),
        respond: () => ({
          json: { roundId: 'r1', userId: 'p2', hasPredictions: false, predictions: [] },
        }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/manual'),
        respond: (req) => {
          saved.push(req.postDataJSON());
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await page.locator('select').selectOption('p2');

    // The justification is offered without ticking "overwrite" — it is not an overwrite.
    await expect(page.locator('#ow')).not.toBeChecked();
    await fillScores(page, ['1', '1', '0', '2']);
    await page.getByPlaceholder('Justificativa').fill('Reativado pela direção.');
    await page.getByRole('button', { name: 'Salvar palpites' }).click();

    await expect(page.locator('.toast-body')).toHaveText('Palpites salvos!');
    expect(saved[0]).toMatchObject({
      allowAfterDeadline: true,
      justification: 'Reativado pela direção.',
    });
  });
});
