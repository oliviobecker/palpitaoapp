import { expect, test } from '@playwright/test';
import { installApi, participants, path, round, seedAuth } from './support';

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

  test('a participant with no predictions starts blank and not overwriting', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
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
    ]);

    await page.goto('/admin/rounds/r1/manual-predictions');
    await page.locator('select').selectOption('p2');

    await expect(page.locator('#ow')).not.toBeChecked();
    await expect(
      page.getByText('Palpites carregados — salvar irá sobrescrever os existentes.'),
    ).toHaveCount(0);
    await expect(page.locator('input[type="number"]').nth(0)).toHaveValue('0');
  });
});
