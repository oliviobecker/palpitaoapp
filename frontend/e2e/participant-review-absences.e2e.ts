import { expect, Page, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

interface ReviewBody {
  justification: string;
  rounds: { roundId: string; isAbsent: boolean }[];
}

function participants() {
  return [
    {
      id: 'u1',
      name: 'João Paulo',
      email: 'joao@x.com',
      isActive: true,
      isEliminated: true,
      totalPoints: 0,
      absenceCount: 5,
      penaltyPoints: 40,
    },
  ];
}

/** Two scored rounds he never predicted (the 2nd already costing 20) and one still locked. */
function reviewRounds() {
  return [
    {
      roundId: 'r1',
      number: 1,
      title: null,
      status: 'Scored',
      matchCount: 10,
      predictionCount: 0,
      isAbsent: true,
      hasOverride: false,
      requiresRecalculation: true,
      absenceNumber: 1,
      penaltyPoints: 0,
    },
    {
      roundId: 'r2',
      number: 2,
      title: 'Boxing Day',
      status: 'Scored',
      matchCount: 10,
      predictionCount: 0,
      isAbsent: true,
      hasOverride: false,
      requiresRecalculation: true,
      absenceNumber: 3,
      penaltyPoints: 20,
    },
    {
      roundId: 'r3',
      number: 3,
      title: null,
      status: 'Locked',
      matchCount: 10,
      predictionCount: 0,
      isAbsent: true,
      hasOverride: false,
      requiresRecalculation: false,
      absenceNumber: null,
      penaltyPoints: null,
    },
  ];
}

function openReview(page: Page) {
  return page
    .locator('.card', { hasText: 'João Paulo' })
    .getByRole('button', { name: /^Revisar ausências$/ })
    .click();
}

test.describe('Reviewing a participant’s absences in closed rounds', () => {
  test('unticks the scored rounds, sends explicit decisions and reports the replay', async ({
    page,
  }) => {
    await seedAuth(page, 'pt-BR');
    const posted: ReviewBody[] = [];

    await installApi(page, [
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants() }) },
      {
        method: 'GET',
        match: (p) => /\/admin\/users\/.+\/absence-review$/.test(p),
        respond: () => ({ json: reviewRounds() }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/users\/.+\/absence-review$/.test(p),
        respond: (req) => {
          posted.push(req.postDataJSON() as ReviewBody);
          return { json: { changedRounds: 2, recalculated: true } };
        },
      },
    ]);

    await page.goto('/admin/participants');
    await expect(page.getByText('joao@x.com')).toBeVisible();
    await openReview(page);

    const modal = page.locator('.modal');
    await expect(modal).toBeVisible();
    await expect(modal).toContainText('Contar como ausente em:');
    await expect(modal).toContainText('a temporada será recalculada');
    await expect(modal).toContainText('Rodada 2 — Boxing Day');
    await expect(modal).toContainText('3ª ausência (punição: 20)');
    await expect(modal).toContainText('vale quando ela for pontuada');

    // Every round opens in today's state: absent.
    for (const id of ['r1', 'r2', 'r3']) {
      await expect(modal.locator(`#confirm-dialog-choice-${id}`)).toBeChecked();
    }

    // He had not joined yet for rounds 1 and 2.
    await modal.locator('#confirm-dialog-choice-r1').uncheck();
    await modal.locator('#confirm-dialog-choice-r2').uncheck();

    // The justification is mandatory.
    await expect(modal.locator('.btn-primary')).toBeDisabled();
    await modal.locator('#confirm-dialog-input').fill('Ainda não participava do bolão.');
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText(
      'Ausências revisadas e temporada recalculada.',
    );
    expect(posted).toEqual([
      {
        justification: 'Ainda não participava do bolão.',
        rounds: [
          { roundId: 'r1', isAbsent: false },
          { roundId: 'r2', isAbsent: false },
          { roundId: 'r3', isAbsent: true },
        ],
      },
    ]);
  });

  test('shows an info toast and no dialog when there is nothing to review', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const posted: ReviewBody[] = [];

    await installApi(page, [
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants() }) },
      {
        method: 'GET',
        match: (p) => /\/admin\/users\/.+\/absence-review$/.test(p),
        respond: () => ({ json: [] }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/users\/.+\/absence-review$/.test(p),
        respond: (req) => {
          posted.push(req.postDataJSON() as ReviewBody);
          return { json: { changedRounds: 0, recalculated: false } };
        },
      },
    ]);

    await page.goto('/admin/participants');
    await openReview(page);

    await expect(page.locator('.toast-body')).toHaveText(
      'Nenhuma rodada encerrada com ausência para revisar.',
    );
    await expect(page.locator('.modal')).toHaveCount(0);
    expect(posted).toEqual([]);
  });

  test('confirming the dialog untouched reports that nothing changed', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const posted: ReviewBody[] = [];

    await installApi(page, [
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants() }) },
      {
        method: 'GET',
        match: (p) => /\/admin\/users\/.+\/absence-review$/.test(p),
        respond: () => ({ json: reviewRounds() }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/users\/.+\/absence-review$/.test(p),
        respond: (req) => {
          posted.push(req.postDataJSON() as ReviewBody);
          return { json: { changedRounds: 0, recalculated: false } };
        },
      },
    ]);

    await page.goto('/admin/participants');
    await openReview(page);

    const modal = page.locator('.modal');
    await modal.locator('#confirm-dialog-input').fill('Conferido.');
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Nenhuma alteração nas ausências.');
    expect(posted).toEqual([
      {
        justification: 'Conferido.',
        rounds: [
          { roundId: 'r1', isAbsent: true },
          { roundId: 'r2', isAbsent: true },
          { roundId: 'r3', isAbsent: true },
        ],
      },
    ]);
  });
});
