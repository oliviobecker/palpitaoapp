import { expect, test } from '@playwright/test';
import { installApi, path, round, seedAuth } from './support';

/**
 * An absence used to mean "did not predict every match", so 1 of 2 was punished exactly like
 * sending nothing: round zeroed, a rung up the ladder, elimination at the 5th. Now the panel
 * has to show the two states apart — and offer the override that the API had exposed since the
 * absence module while no screen ever called it.
 *
 * `Locked` on purpose: that is the window where the flag still matters, between the lock and
 * the scoring pass that turns it into a punishment.
 */
const lockedRound = { ...round, status: 'Locked', lockedAt: '2026-01-02T00:00:00Z' };

const coverage = {
  roundId: 'r1',
  matchCount: 2,
  totalParticipants: 3,
  completeParticipants: 1,
  missing: [
    {
      userId: 'p1',
      name: 'João Silva',
      predictedCount: 1,
      willBeAbsent: false,
      hasOverride: false,
    },
    {
      userId: 'p2',
      name: 'Maria Souza',
      predictedCount: 0,
      willBeAbsent: true,
      hasOverride: false,
    },
  ],
};

interface OverrideBody {
  userId: string;
  isAbsent: boolean;
  justification: string;
}

test.describe('Absence override on the round detail', () => {
  test('tells incomplete from absent and excuses a participant', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const posted: OverrideBody[] = [];

    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: lockedRound }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/coverage'),
        respond: () => ({ json: coverage }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/absences/override'),
        respond: (req) => {
          posted.push(req.postDataJSON() as OverrideBody);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1');

    // Both are missing a prediction; only one of them is heading for an absence.
    const incomplete = page.locator('.alert li', { hasText: 'João Silva' });
    const silent = page.locator('.alert li', { hasText: 'Maria Souza' });
    await expect(incomplete).toContainText('(1/2)');
    await expect(incomplete).toContainText('Incompleto');
    await expect(incomplete).not.toContainText('Ficará ausente');
    await expect(silent).toContainText('(0/2)');
    await expect(silent).toContainText('Ficará ausente');

    await silent.getByRole('button', { name: 'Marcar presente' }).click();

    const modal = page.locator('.modal');
    await expect(modal).toBeVisible();
    await expect(modal).toContainText('Maria Souza');
    await modal.locator('#confirm-dialog-input').fill('Avisou por telefone antes do fechamento.');
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Presença atualizada.');
    expect(posted).toEqual([
      { userId: 'p2', isAbsent: false, justification: 'Avisou por telefone antes do fechamento.' },
    ]);
  });

  test('on a part, the toggle follows this part even when the round is not an absence', async ({
    page,
  }) => {
    // Maria sent nothing in 10.2 but did send 10.1: present in the round, absent in this part.
    // The override the button writes is this part's, so it must offer "present" — reading the
    // round's verdict instead would post the opposite of what the admin sees.
    await seedAuth(page, 'pt-BR');
    const posted: OverrideBody[] = [];
    const part = { ...lockedRound, number: 10, part: 2 };
    const partCoverage = {
      ...coverage,
      missing: [
        {
          userId: 'p2',
          name: 'Maria Souza',
          predictedCount: 0,
          willBeAbsent: false,
          absentInPart: true,
          hasOverride: false,
        },
      ],
    };

    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: part }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/predictions/coverage'),
        respond: () => ({ json: partCoverage }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/absences/override'),
        respond: (req) => {
          posted.push(req.postDataJSON() as OverrideBody);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1');

    const row = page.locator('.alert li', { hasText: 'Maria Souza' });
    await expect(row).toContainText('Sem palpites nesta parte');
    await expect(row).not.toContainText('Ficará ausente');

    await row.getByRole('button', { name: 'Marcar presente' }).click();
    const modal = page.locator('.modal');
    await modal.locator('#confirm-dialog-input').fill('Mandou pelo WhatsApp.');
    await modal.locator('.btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Presença atualizada.');
    expect(posted).toEqual([
      { userId: 'p2', isAbsent: false, justification: 'Mandou pelo WhatsApp.' },
    ]);
  });
});
