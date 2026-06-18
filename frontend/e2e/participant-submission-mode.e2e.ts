import { expect, Page, test } from '@playwright/test';
import { installApi, path, round } from './support';

/** Seeds an authenticated participant + group context. */
async function seedParticipant(page: Page): Promise<void> {
  await page.addInitScript(() => {
    localStorage.setItem('palpitao.token', 'e2e-fake-jwt');
    localStorage.setItem(
      'palpitao.user',
      JSON.stringify({
        id: 'p1',
        name: 'João Silva',
        email: 'joao@x.com',
        role: 'Participant',
        isActive: true,
      }),
    );
    localStorage.setItem('palpitao.groupId', 'g1');
    localStorage.setItem('palpitao.groupName', 'Palpitão England 2025/2026');
    localStorage.setItem('palpitao.groupRole', 'Participant');
    localStorage.setItem('palpitao.lang', 'en-US');
  });
}

const mine = {
  roundId: 'r1',
  status: 'Published',
  firstMatchStartsAt: round.firstMatchStartsAt,
  predictions: [],
};

/** The submission-mode flag rides on the round (resolved from its season). */
function predictionHandlers(allowSubmit: boolean) {
  return [
    {
      method: 'GET',
      match: path('/rounds/r1'),
      respond: () => ({ json: { ...round, allowParticipantsToSubmitPredictions: allowSubmit } }),
    },
    { method: 'GET', match: path('/rounds/r1/predictions/me'), respond: () => ({ json: mine }) },
  ];
}

test.describe('Participant submission mode', () => {
  test('shows the save button when participants can submit in the app', async ({ page }) => {
    await seedParticipant(page);
    await installApi(page, predictionHandlers(true));

    await page.goto('/rounds/r1/predictions');

    await expect(page.getByRole('button', { name: /save predictions/i })).toBeVisible();
  });

  test('hides the save button and shows a notice in admin-only mode', async ({ page }) => {
    await seedParticipant(page);
    await installApi(page, predictionHandlers(false));

    await page.goto('/rounds/r1/predictions');

    await expect(
      page.getByText('In this season, predictions are entered by the administrator.', {
        exact: false,
      }),
    ).toBeVisible();
    await expect(page.getByRole('button', { name: /save predictions/i })).toHaveCount(0);
  });
});
