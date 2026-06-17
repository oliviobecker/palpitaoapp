import { expect, Page, test } from '@playwright/test';
import { API, installApi, path } from './support';

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

/** A locked round; the season's visibility flag rides on the round summary. */
function lockedRound(allowViewOthers: boolean) {
  return {
    id: 'r1',
    seasonId: 's1',
    number: 5,
    title: null,
    status: 'Locked',
    firstMatchStartsAt: '2026-01-01T18:00:00Z',
    publishedAt: '2026-01-01T00:00:00Z',
    lockedAt: '2026-01-01T18:00:00Z',
    matchCount: 1,
    allowParticipantsToViewOthersPredictions: allowViewOthers,
    allowParticipantsToSubmitPredictions: true,
  };
}

/** An open (Published) round; live visibility opens the mirror before the lock. */
function publishedRound(allowViewOthers: boolean) {
  return {
    id: 'r1',
    seasonId: 's1',
    number: 5,
    title: null,
    status: 'Published',
    firstMatchStartsAt: '2999-01-01T18:00:00Z',
    publishedAt: '2026-01-01T00:00:00Z',
    lockedAt: null,
    matchCount: 1,
    allowParticipantsToViewOthersPredictions: allowViewOthers,
    allowParticipantsToSubmitPredictions: true,
  };
}

const mirror = {
  roundId: 'r1',
  status: 'Locked',
  matches: [
    {
      roundMatchId: 'm1',
      competition: 'PremierLeague',
      phase: 'Regular',
      homeTeamName: 'Arsenal',
      awayTeamName: 'Chelsea',
      startsAt: '2026-01-01T18:00:00Z',
    },
  ],
  participants: [
    {
      userId: 'p1',
      name: 'João Silva',
      isAbsent: false,
      isEliminated: false,
      flavioRuleApplied: false,
      predictions: [
        {
          roundMatchId: 'm1',
          predictedHomeScore: 2,
          predictedAwayScore: 1,
          submittedAt: '2026-01-01T10:00:00Z',
        },
      ],
    },
    {
      userId: 'p2',
      name: 'Maria Souza',
      isAbsent: true,
      isEliminated: false,
      flavioRuleApplied: false,
      predictions: [],
    },
  ],
};

test.describe("Participants view others' predictions", () => {
  test('hides the View predictions button when the season disables it', async ({ page }) => {
    await seedParticipant(page);
    await installApi(page, [
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [lockedRound(false)] }) },
    ]);

    await page.goto('/rounds');

    await expect(page.getByText('Round 5')).toBeVisible();
    await expect(page.getByRole('link', { name: 'View predictions' })).toHaveCount(0);
  });

  test('shows the button when enabled and opens the mirror', async ({ page }) => {
    await seedParticipant(page);
    await installApi(page, [
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [lockedRound(true)] }) },
      { method: 'GET', match: path('/rounds/r1/mirror'), respond: () => ({ json: mirror }) },
    ]);

    await page.goto('/rounds');
    await page.getByRole('link', { name: 'View predictions' }).click();

    await expect(page.getByRole('heading', { name: "Participants' predictions" })).toBeVisible();
    // Names appear as <strong> inside each participant card.
    await expect(page.getByRole('strong').filter({ hasText: 'João Silva' })).toBeVisible();
    await expect(page.getByRole('strong').filter({ hasText: 'Maria Souza' })).toBeVisible();
    await expect(page.getByText('2 - 1')).toBeVisible();
  });

  test('shows the View predictions button on an OPEN round when enabled (live)', async ({
    page,
  }) => {
    await seedParticipant(page);
    await installApi(page, [
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [publishedRound(true)] }) },
      {
        method: 'GET',
        match: path('/rounds/r1/mirror'),
        respond: () => ({ json: { ...mirror, status: 'Published' } }),
      },
    ]);

    await page.goto('/rounds');
    // The button shows even though the round is still open (not locked).
    await page.getByRole('link', { name: 'View predictions' }).click();

    await expect(page.getByRole('heading', { name: "Participants' predictions" })).toBeVisible();
    await expect(page.getByRole('strong').filter({ hasText: 'Maria Souza' })).toBeVisible();
  });

  test('hides the button on an OPEN round when disabled', async ({ page }) => {
    await seedParticipant(page);
    await installApi(page, [
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [publishedRound(false)] }) },
    ]);

    await page.goto('/rounds');

    await expect(page.getByText('Round 5')).toBeVisible();
    await expect(page.getByRole('link', { name: 'View predictions' })).toHaveCount(0);
  });

  test('shows a friendly message when the API forbids access (403)', async ({ page }) => {
    await seedParticipant(page);
    await installApi(page, [
      {
        method: 'GET',
        match: path('/rounds/r1/mirror'),
        respond: () => ({ status: 403, json: { message: 'no' } }),
      },
    ]);

    await page.goto('/rounds/r1/mirror');

    await expect(
      page.getByText("You do not have permission to view other participants' predictions."),
    ).toBeVisible();
  });
});
