import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const englandSeason = {
  id: 's1',
  name: 'England 2025/2026',
  startDate: '2025-08-01',
  endDate: '2026-05-31',
  isActive: true,
  tournamentType: 'PalpitaoEngland',
  allowParticipantsToViewOthersPredictions: false,
  allowParticipantsToSubmitPredictions: true,
  faCupEnabled: true,
  hasParticipantPredictions: false,
};

test.describe('Season FA Cup toggle', () => {
  test('creates a season with the FA Cup turned off', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const created: Array<Record<string, unknown>> = [];
    await installApi(page, [
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: [englandSeason] }) },
      {
        method: 'POST',
        match: path('/seasons'),
        respond: (req) => {
          created.push(req.postDataJSON());
          return { status: 201, json: { ...englandSeason, id: 's2', faCupEnabled: false } };
        },
      },
    ]);

    await page.goto('/admin/seasons');

    const toggle = page.locator('#faCup');
    await expect(toggle).toBeVisible();
    await expect(toggle).toBeChecked(); // on by default

    await page.fill('input[formControlName="name"]', 'England 2026/2027');
    await page.fill('input[formControlName="startDate"]', '2026-08-01');
    await page.fill('input[formControlName="endDate"]', '2027-05-31');
    await toggle.uncheck();

    await page.getByRole('button', { name: 'Criar', exact: true }).click();

    await expect.poll(() => created.length).toBe(1);
    expect(created[0]).toMatchObject({ faCupEnabled: false });
  });

  test('hides the toggle for a World Cup certame', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: [englandSeason] }) },
    ]);

    await page.goto('/admin/seasons');
    await expect(page.locator('#faCup')).toBeVisible();

    // The World Cup certame never runs the FA Cup, so the option does not apply.
    await page.getByRole('button', { name: /Copa do Mundo/ }).click();
    await expect(page.locator('#faCup')).toHaveCount(0);
  });
});
