import { expect, Page, test } from '@playwright/test';
import { API, installApi, path } from './support';

/** Seeds an authenticated participant + group context, with the visibility flag. */
async function seedParticipant(page: Page, allowViewOthers: boolean): Promise<void> {
  await page.addInitScript(
    ({ allow }) => {
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
      localStorage.setItem('palpitao.groupViewOthers', String(allow));
      localStorage.setItem('palpitao.lang', 'en-US');
    },
    { allow: allowViewOthers },
  );
}

const lockedRound = {
  id: 'r1',
  seasonId: 's1',
  number: 5,
  title: null,
  status: 'Locked',
  firstMatchStartsAt: '2026-01-01T18:00:00Z',
  publishedAt: '2026-01-01T00:00:00Z',
  lockedAt: '2026-01-01T18:00:00Z',
  matchCount: 1,
};

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
  test('hides the View predictions button when the group disables it', async ({ page }) => {
    await seedParticipant(page, false);
    await installApi(page, [
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [lockedRound] }) },
    ]);

    await page.goto('/rounds');

    await expect(page.getByText('Round 5')).toBeVisible();
    await expect(page.getByRole('link', { name: 'View predictions' })).toHaveCount(0);
  });

  test('shows the button when enabled and opens the mirror', async ({ page }) => {
    await seedParticipant(page, true);
    await installApi(page, [
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [lockedRound] }) },
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

  test('shows a friendly message when the API forbids access (403)', async ({ page }) => {
    await seedParticipant(page, true);
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
