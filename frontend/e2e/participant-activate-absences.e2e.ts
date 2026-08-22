import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

interface ActivateBody {
  absentRoundIds: string[];
}

function participants(isActive: boolean) {
  return [
    {
      id: 'u1',
      name: 'João Paulo',
      email: 'joao@x.com',
      isActive,
      isEliminated: false,
      totalPoints: 0,
      absenceCount: 0,
      penaltyPoints: 0,
    },
  ];
}

/** Round 1 already closed for predictions; round 2 was scored while he was out. */
function candidates() {
  return [
    {
      roundId: 'r1',
      number: 1,
      title: null,
      status: 'Locked',
      matchCount: 10,
      predictionCount: 0,
      requiresRescore: false,
      hasPresentOverride: false,
    },
    {
      roundId: 'r2',
      number: 2,
      title: null,
      status: 'Scored',
      matchCount: 10,
      predictionCount: 0,
      requiresRescore: true,
      hasPresentOverride: false,
    },
  ];
}

test.describe('Activating a participant records the absences they missed', () => {
  test('pre-ticks the locked round, leaves the scored one out, and posts the choice', async ({
    page,
  }) => {
    await seedAuth(page, 'pt-BR');
    let active = false;
    const posted: ActivateBody[] = [];

    await installApi(page, [
      {
        method: 'GET',
        match: path('/admin/users'),
        respond: () => ({ json: participants(active) }),
      },
      {
        method: 'GET',
        match: (p) => /\/admin\/users\/.+\/absence-candidates$/.test(p),
        respond: () => ({ json: candidates() }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/users\/.+\/activate$/.test(p),
        respond: (req) => {
          posted.push(req.postDataJSON() as ActivateBody);
          active = true;
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/participants');
    await expect(page.getByText('joao@x.com')).toBeVisible();

    await page
      .locator('.card', { hasText: 'João Paulo' })
      .getByRole('button', { name: /^Ativar$/ })
      .click();

    const modal = page.locator('.modal');
    await expect(modal).toBeVisible();
    await expect(modal).toContainText('Registrar ausência em:');

    // The locked round starts ticked (its absence lands on its own at scoring time);
    // the scored one does not, because it would need a deliberate re-score.
    const locked = modal.locator('#confirm-dialog-choice-r1');
    const scored = modal.locator('#confirm-dialog-choice-r2');
    await expect(locked).toBeChecked();
    await expect(scored).not.toBeChecked();
    await expect(modal).toContainText('recalcule-a para a ausência valer');

    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Participante ativado.');
    expect(posted).toEqual([{ absentRoundIds: ['r1'] }]);
  });

  test('activates with no absences when the admin unticks everything', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const posted: ActivateBody[] = [];

    await installApi(page, [
      {
        method: 'GET',
        match: path('/admin/users'),
        respond: () => ({ json: participants(false) }),
      },
      {
        method: 'GET',
        match: (p) => /\/admin\/users\/.+\/absence-candidates$/.test(p),
        respond: () => ({ json: candidates() }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/users\/.+\/activate$/.test(p),
        respond: (req) => {
          posted.push(req.postDataJSON() as ActivateBody);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/participants');
    await page
      .locator('.card', { hasText: 'João Paulo' })
      .getByRole('button', { name: /^Ativar$/ })
      .click();

    const modal = page.locator('.modal');
    await modal.locator('#confirm-dialog-choice-r1').uncheck();
    await modal.locator('.btn-primary').click();

    // Unticking everything still activates — it is not the same as cancelling.
    expect(posted).toEqual([{ absentRoundIds: [] }]);
  });

  test('skips the dialog when no round closed while they were out', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const posted: ActivateBody[] = [];

    await installApi(page, [
      {
        method: 'GET',
        match: path('/admin/users'),
        respond: () => ({ json: participants(false) }),
      },
      {
        method: 'GET',
        match: (p) => /\/admin\/users\/.+\/absence-candidates$/.test(p),
        respond: () => ({ json: [] }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/users\/.+\/activate$/.test(p),
        respond: (req) => {
          posted.push(req.postDataJSON() as ActivateBody);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/participants');
    await page
      .locator('.card', { hasText: 'João Paulo' })
      .getByRole('button', { name: /^Ativar$/ })
      .click();

    await expect(page.locator('.toast-body')).toHaveText('Participante ativado.');
    await expect(page.locator('.modal')).toHaveCount(0);
    expect(posted).toEqual([{ absentRoundIds: [] }]);
  });
});
