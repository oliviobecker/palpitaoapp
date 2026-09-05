import { Request } from '@playwright/test';
import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const KEY = 'A7C39F2E4BD8';

const SEASON = {
  groupName: 'Turma do Zé',
  seasonName: 'England 2025/26',
  tournamentType: 'PalpitaoEngland',
  rounds: [
    { number: 18, title: null, status: 'Scored', startDate: null, endDate: null, isScored: true },
    { number: 17, title: null, status: 'Locked', startDate: null, endDate: null, isScored: false },
  ],
  ruleset: { columnOnly: 1, traditional: 3, medium: 5, uncommon: 7, extraUncommon: 10 },
};

const STANDINGS = [
  {
    position: 1,
    userId: 'u1',
    name: 'Flávio Barros',
    totalPoints: 187,
    playedRounds: 17,
    absenceCount: 0,
    penaltyPoints: 0,
    isEliminated: false,
    rounds: [
      { number: 17, points: 9, wasAbsent: false, flavioRuleApplied: false },
      { number: 18, points: 6, wasAbsent: false, flavioRuleApplied: false },
    ],
  },
  {
    position: 2,
    userId: 'u2',
    name: 'Ana Prado',
    totalPoints: 181,
    playedRounds: 17,
    absenceCount: 1,
    penaltyPoints: 0,
    isEliminated: false,
    rounds: [{ number: 18, points: 4, wasAbsent: true, flavioRuleApplied: false }],
  },
];

const ROUND = {
  number: 18,
  title: null,
  status: 'Scored',
  isPartial: false,
  computedMatches: 1,
  remainingMatches: 0,
  lastUpdatedAt: null,
  matches: [
    {
      roundMatchId: 'm1',
      competition: 'PremierLeague',
      phase: 'Regular',
      homeTeamName: 'Arsenal',
      awayTeamName: 'Chelsea',
      homeScore: 2,
      awayScore: 1,
      isFinished: true,
      multiplier: 2,
      isClassic: true,
      isManualMultiplier: false,
    },
  ],
  participants: [
    {
      userId: 'u1',
      name: 'Flávio Barros',
      grossPoints: 6,
      finalPoints: 6,
      penaltyPoints: 0,
      wasAbsent: false,
      wasEliminated: false,
      flavioRuleApplied: false,
      matchScores: [
        {
          roundMatchId: 'm1',
          predictedHomeScore: 2,
          predictedAwayScore: 1,
          basePoints: 3,
          multiplier: 2,
          finalPoints: 6,
          scoreCategory: 'Traditional',
          isExactScore: true,
          isCorrectColumn: true,
        },
      ],
    },
  ],
};

/** Records every request the page makes to the public API, headers included. */
function publicApi(seen: Request[]) {
  return [
    {
      method: 'GET' as const,
      match: path(`/public/seasons/${KEY}`),
      respond: (req: Request) => {
        seen.push(req);
        return { json: SEASON };
      },
    },
    {
      method: 'GET' as const,
      match: path(`/public/seasons/${KEY}/standings`),
      respond: (req: Request) => {
        seen.push(req);
        return { json: STANDINGS };
      },
    },
    {
      method: 'GET' as const,
      match: path(`/public/seasons/${KEY}/rounds/18`),
      respond: (req: Request) => {
        seen.push(req);
        return { json: ROUND };
      },
    },
  ];
}

test.describe('Public standings link', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));
  });

  test('opens by key with no account and shows the standings', async ({ page }) => {
    await installApi(page, publicApi([]));

    await page.goto(`/p/${KEY}`);

    await expect(page.getByText('Turma do Zé')).toBeVisible();
    await expect(page.getByText('England 2025/26')).toBeVisible();
    await expect(page.getByText('Flávio Barros')).toBeVisible();
    await expect(page.getByText('187')).toBeVisible();
  });

  test('accepts the key hyphenated and via the query string', async ({ page }) => {
    await installApi(page, publicApi([]));

    await page.goto('/p?key=a7c3-9f2e-4bd8');

    await expect(page.getByText('England 2025/26')).toBeVisible();
  });

  test('expands a participant and shows how each point was earned', async ({ page }) => {
    await installApi(page, publicApi([]));
    await page.goto(`/p/${KEY}?rodada=18`);

    await page.getByRole('button', { name: /Flávio Barros/ }).click();

    await expect(page.getByText('Arsenal')).toBeVisible();
    await expect(page.getByText('Clássico')).toBeVisible();
    // The whole point of the screen: the arithmetic is on display, next to the prediction.
    // Exact: the legend at the foot of the page also spells out "Tradicional: 3".
    await expect(page.getByText('Tradicional', { exact: true })).toBeVisible();
    await expect(page.getByText('3 × 2 =')).toBeVisible();
    await expect(page.getByText('+6')).toBeVisible();
  });

  test('sends no session or tenant headers, even when signed into another group', async ({
    page,
  }) => {
    // A logged-in admin of some other group must still be able to read a shared link.
    await seedAuth(page, 'pt-BR');
    const seen: Request[] = [];
    await installApi(page, publicApi(seen));

    await page.goto(`/p/${KEY}`);
    await expect(page.getByText('Flávio Barros')).toBeVisible();

    expect(seen.length).toBeGreaterThan(0);
    for (const req of seen) {
      const headers = req.headers();
      expect(headers['x-group-id']).toBeUndefined();
      expect(headers['authorization']).toBeUndefined();
    }
  });

  test('explains a dead link instead of showing a generic error', async ({ page }) => {
    await installApi(page, [
      {
        method: 'GET',
        match: path(`/public/seasons/${KEY}`),
        respond: () => ({ status: 404, json: { status: 404, message: 'notFound.season' } }),
      },
    ]);

    await page.goto(`/p/${KEY}`);

    await expect(page.getByText(/Link inválido ou desativado/)).toBeVisible();
    // ...and only that: the generic error toast must not pile on top of the explanation.
    await expect(page.locator('.toast')).toHaveCount(0);
  });

  test('offers a retry when the round fails to load', async ({ page }) => {
    let attempts = 0;
    await installApi(page, [
      ...publicApi([]).slice(0, 2),
      {
        method: 'GET' as const,
        match: path(`/public/seasons/${KEY}/rounds/18`),
        respond: () => {
          attempts += 1;
          return attempts === 1 ? { status: 500, json: {} } : { json: ROUND };
        },
      },
    ]);

    await page.goto(`/p/${KEY}?rodada=18`);

    // Previously this left the tab blank, with no way forward short of reloading the page.
    const retry = page.getByRole('button', { name: /Tentar novamente|Try again|Recarregar/i });
    await expect(retry).toBeVisible();
    await retry.click();
    await expect(page.getByText('Flávio Barros')).toBeVisible();
  });

  test('keeps the page out of search indexes', async ({ page }) => {
    await installApi(page, publicApi([]));

    await page.goto(`/p/${KEY}`);
    await expect(page.getByText('Flávio Barros')).toBeVisible();

    // The API's X-Robots-Tag never reaches a crawler: what gets indexed is this document.
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
  });

  test('pivots the round by match and shows everyone who predicted it', async ({ page }) => {
    await installApi(page, publicApi([]));
    await page.goto(`/p/${KEY}?rodada=18`);

    await page.getByRole('button', { name: 'Por jogo' }).click();

    // Collapsed, the match card already answers "did anyone get this one?".
    const card = page.getByRole('button', { name: /Arsenal/ });
    await expect(card).toBeVisible();
    await card.click();

    // Open, it is the same arithmetic as the participant cut, transposed.
    await expect(page.getByText('Flávio Barros')).toBeVisible();
    await expect(page.getByText('3 × 2 =')).toBeVisible();
  });

  test('opens a round straight from the history strip', async ({ page }) => {
    await installApi(page, publicApi([]));
    await page.goto(`/p/${KEY}`);

    await page.getByRole('button', { name: /Flávio Barros/ }).click();
    // The strip carries one chip per scored round; pressing one is a deep link.
    await page.getByRole('button', { name: /17.*9/ }).click();

    // The chip is a deep link: it carries both the round and the participant, so the URL
    // that gets shared reproduces exactly what the reader is looking at.
    await expect(page).toHaveURL(/rodada=17/);
    await expect(page).toHaveURL(/participante=u1/);
  });

  test('remembers which row the reader claimed, across a reload', async ({ page }) => {
    await installApi(page, publicApi([]));
    await page.goto(`/p/${KEY}`);

    await page.getByRole('button', { name: /Ana Prado/ }).click();
    await page.getByRole('button', { name: 'Sou eu' }).click();
    await expect(page.getByText('Você')).toBeVisible();

    await page.reload();

    // No session is involved: the choice lives in this browser and nowhere else.
    await expect(page.getByText('Você')).toBeVisible();
  });

  test('says so when the link points at a round that is not published', async ({ page }) => {
    await installApi(page, publicApi([]));

    await page.goto(`/p/${KEY}?rodada=99`);

    // Previously this fell back to the newest round with no explanation at all.
    await expect(page.getByRole('status')).toContainText('99');
  });
});
