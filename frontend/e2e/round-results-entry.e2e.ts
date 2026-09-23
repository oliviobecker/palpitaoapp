import { expect, test } from '@playwright/test';
import { installApi, path, round, seedAuth } from './support';

/**
 * Production, round 3: the results refresh stored Aston Villa x Arsenal at 0x0 four minutes into
 * the match. The results editor shows whatever score a match holds — and used to send every
 * filled pair on save, which turns a live score into a manual *final* result that the refresh
 * then never touches again. Only what the admin types is sent now.
 */
const lockedRound = {
  ...round,
  status: 'Locked',
  lockedAt: '2026-01-02T00:00:00Z',
  matches: [
    { ...round.matches[0], homeScore: 0, awayScore: 0, status: 'InProgress', isFinished: false },
    { ...round.matches[1] },
  ],
};

test.describe('Results entry on the round detail', () => {
  test('saves only the scores the admin typed, never a live one it merely shows', async ({
    page,
  }) => {
    await seedAuth(page, 'pt-BR');
    const saved: { id: string; body: unknown }[] = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: lockedRound }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/coverage'),
        respond: () => ({
          json: {
            roundId: 'r1',
            matchCount: 2,
            totalParticipants: 0,
            completeParticipants: 0,
            missing: [],
          },
        }),
      },
      {
        method: 'POST',
        match: (p) => /\/matches\/m\d\/result$/.test(p),
        respond: (req) => {
          saved.push({ id: req.url().split('/').at(-2)!, body: req.postDataJSON() });
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1');
    const editor = page.locator('app-round-results-editor');

    // The live match is marked as such; its score is shown, not yet anybody's result.
    await expect(editor.getByText('Em andamento')).toBeVisible();
    await expect(editor.locator('[data-score="0-home"]')).toHaveValue('0');

    // Nothing typed, nothing to save.
    const save = editor.getByRole('button', { name: /Salvar resultados/ });
    await expect(save).toBeDisabled();

    await editor.locator('[data-score="1-home"]').fill('2');
    await editor.locator('[data-score="1-away"]').fill('1');
    await save.click();

    await expect.poll(() => saved).toEqual([{ id: 'm2', body: { homeScore: 2, awayScore: 1 } }]);
  });
});
