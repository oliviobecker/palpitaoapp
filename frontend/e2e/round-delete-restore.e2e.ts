import { expect, test } from '@playwright/test';
import { installApi, path, round, seedAuth } from './support';

/**
 * A cancelled round can be restored to the status it was cancelled from, and a Draft or
 * Cancelled round can be deleted for good — the later rounds close the gap and, when a scored
 * round is involved, the season is replayed, so the dialog spells that out.
 */

const noMove = { allowed: false, renumberedRounds: 0, requiresRecalculation: false };

const cancelledRound = {
  ...round,
  id: 'r9',
  number: 9,
  part: 0,
  status: 'Cancelled',
  lockedAt: '2026-01-02T00:00:00Z',
  restoreStatus: 'Locked',
  week: {
    parts: [{ id: 'r9', number: 9, part: 0, status: 'Cancelled' }],
    decidesAbsences: true,
    openPartBlocksFinalize: false,
    laterPartScored: false,
    joinPrevious: noMove,
    leave: noMove,
    delete: { allowed: true, renumberedRounds: 2, requiresRecalculation: true },
  },
};

/** The second list of week 7, cancelled while the first one is already scored. */
const cancelledPart = {
  ...cancelledRound,
  number: 7,
  part: 2,
  week: {
    ...cancelledRound.week,
    parts: [
      { id: 'r7', number: 7, part: 1, status: 'Scored' },
      { id: 'r9', number: 7, part: 2, status: 'Cancelled' },
    ],
    delete: { allowed: true, renumberedRounds: 1, requiresRecalculation: true },
  },
};

const restoredPart = {
  ...cancelledPart,
  status: 'Locked',
  restoreStatus: null,
  week: {
    ...cancelledPart.week,
    parts: [
      { id: 'r7', number: 7, part: 1, status: 'Scored' },
      { id: 'r9', number: 7, part: 2, status: 'Locked' },
    ],
    delete: noMove,
  },
};

const coverage = (id: string) => ({
  method: 'GET',
  match: path(`/admin/rounds/${id}/predictions/coverage`),
  respond: () => ({
    json: {
      roundId: id,
      matchCount: 2,
      totalParticipants: 0,
      completeParticipants: 0,
      missing: [],
    },
  }),
});

test.describe('Deleting and restoring rounds', () => {
  test('deletes a cancelled round and goes back to the round list', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let deleted = false;

    await installApi(page, [
      { method: 'GET', match: path('/rounds/r9'), respond: () => ({ json: cancelledRound }) },
      {
        method: 'DELETE',
        match: path('/rounds/r9'),
        respond: () => {
          deleted = true;
          return { status: 204 };
        },
      },
      { method: 'GET', match: path('/rounds'), respond: () => ({ json: [] }) },
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: [] }) },
    ]);

    await page.goto('/admin/rounds/r9');

    await page.getByRole('button', { name: 'Excluir rodada' }).click();

    const modal = page.locator('.modal');
    await expect(modal).toContainText('A Rodada 9 será excluída de vez');
    await expect(modal).toContainText('2 rodada(s) mudam de número ou de parte');
    await expect(modal).toContainText('A temporada será recalculada');
    await modal.locator('.btn-danger').click();

    await expect(page).toHaveURL(/\/admin\/rounds$/);
    await expect(page.locator('.toast-body')).toHaveText('Rodada excluída.');
    expect(deleted).toBe(true);
  });

  test('restores a cancelled part to the status it was cancelled from', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let restored = false;

    await installApi(page, [
      coverage('r9'),
      {
        method: 'GET',
        match: path('/rounds/r9'),
        respond: () => ({ json: restored ? restoredPart : cancelledPart }),
      },
      {
        method: 'POST',
        match: path('/rounds/r9/restore'),
        respond: () => {
          restored = true;
          return { json: restoredPart };
        },
      },
    ]);

    await page.goto('/admin/rounds/r9');

    await expect(page.getByText('a rodada volta ao status Bloqueada')).toBeVisible();
    await page.getByRole('button', { name: 'Restaurar rodada' }).click();

    const modal = page.locator('.modal');
    await expect(modal).toContainText('A rodada volta ao status Bloqueada');
    await expect(modal).toContainText('restaurar esta recalcula a temporada');
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Rodada restaurada.');
    expect(restored).toBe(true);
    await expect(page.getByRole('button', { name: 'Restaurar rodada' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Excluir rodada' })).toHaveCount(0);
  });

  test('a published round has to be cancelled before it can be deleted', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const published = {
      ...round,
      week: { ...cancelledRound.week, parts: [], delete: noMove },
    };

    await installApi(page, [
      coverage('r1'),
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: published }) },
    ]);

    await page.goto('/admin/rounds/r1');

    await expect(page.getByRole('button', { name: 'Cancelar rodada' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Excluir rodada' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Restaurar rodada' })).toHaveCount(0);
  });
});
