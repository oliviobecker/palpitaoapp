import { expect, test } from '@playwright/test';
import { installApi, path, round, seedAuth } from './support';

const scoredRound = {
  ...round,
  status: 'Scored',
  matches: round.matches.map((m) => ({ ...m, homeScore: 0, awayScore: 0, isFinished: true })),
};

const panelData = () => ({
  roundId: 'r1',
  applies: true,
  deadlineUtc: '2026-09-01T12:00:00Z',
  participants: [
    {
      userId: 'p1',
      name: 'Vilaça',
      isTarget: true,
      submittedAt: '2026-09-02T10:00:00Z',
      grossPoints: 17,
      finalPoints: 8,
      flavioRuleApplied: true,
      isExempt: false,
      justification: null as string | null,
      updatedAt: null as string | null,
      updatedByUserId: null as string | null,
    },
  ],
});

for (const language of ['pt-BR', 'en-US'] as const) {
  test(`exempt and restore a scored round with season confirmation (${language})`, async ({
    page,
  }, info) => {
    await seedAuth(page, language);
    if (language === 'pt-BR') await page.setViewportSize({ width: 390, height: 844 });
    const data = panelData();
    const bodies: unknown[] = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: scoredRound }) },
      {
        method: 'GET',
        match: path('/rounds/r1/results'),
        respond: () => ({ json: { roundId: 'r1', participants: [] } }),
      },
      { method: 'GET', match: path('/seasons/s1/standings'), respond: () => ({ json: [] }) },
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: [] }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/flavio-overrides'),
        respond: () => ({ json: data }),
      },
      {
        method: 'PUT',
        match: path('/admin/rounds/r1/flavio-overrides'),
        respond: (request) => {
          const body = request.postDataJSON() as {
            userId: string;
            isExempt: boolean;
            justification: string;
          };
          bodies.push(body);
          Object.assign(data.participants[0], {
            isExempt: body.isExempt,
            justification: body.justification,
            finalPoints: body.isExempt ? 17 : 8,
            flavioRuleApplied: !body.isExempt,
            updatedAt: '2026-09-09T14:00:00Z',
            updatedByUserId: 'admin-1',
          });
          return { status: 204 };
        },
      },
    ]);
    await page.goto('/admin/rounds/r1');
    const panel = page.locator('app-admin-flavio-overrides');
    await expect(panel).toContainText('Vilaça');
    await expect(panel).toContainText('01/09/2026 12:00:00 UTC');
    await expect(panel).toContainText('17 / 8');
    await panel
      .getByRole('button', {
        name:
          language === 'pt-BR' ? 'Dispensar regra com justificativa' : 'Exempt with justification',
      })
      .click();
    const modal = page.locator('.modal');
    await expect(modal).toContainText(
      language === 'pt-BR' ? 'todas as rodadas pontuadas' : 'all scored rounds',
    );
    const confirm = modal.locator('.btn-primary');
    await expect(confirm).toBeDisabled();
    await modal.locator('textarea').fill('Recebido pelo WhatsApp antes do prazo.');
    await expect(confirm).toHaveText(
      language === 'pt-BR' ? 'Salvar e recalcular' : 'Save and recalculate',
    );
    await confirm.click();
    await expect(panel).toContainText('17 / 17');
    await expect(panel).toContainText('Recebido pelo WhatsApp antes do prazo.');
    expect(bodies).toEqual([
      { userId: 'p1', isExempt: true, justification: 'Recebido pelo WhatsApp antes do prazo.' },
    ]);
    await panel.scrollIntoViewIfNeeded();
    await page.screenshot({ path: info.outputPath(`flavio-${language}.png`), fullPage: true });
    await panel
      .getByRole('button', {
        name:
          language === 'pt-BR' ? 'Restaurar cálculo automático' : 'Restore automatic calculation',
      })
      .click();
    await modal.locator('textarea').fill('Revisão do comprovante.');
    await confirm.click();
    await expect(panel).toContainText('17 / 8');
    expect(bodies).toHaveLength(2);
    expect(bodies[1]).toEqual({
      userId: 'p1',
      isExempt: false,
      justification: 'Revisão do comprovante.',
    });
  });
}

test('validation, cancellation and failed recalculation preserve the visible result', async ({
  page,
}) => {
  await seedAuth(page, 'pt-BR');
  let attempts = 0;
  await installApi(page, [
    { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: scoredRound }) },
    {
      method: 'GET',
      match: path('/rounds/r1/results'),
      respond: () => ({ json: { participants: [] } }),
    },
    { method: 'GET', match: path('/seasons/s1/standings'), respond: () => ({ json: [] }) },
    { method: 'GET', match: path('/seasons'), respond: () => ({ json: [] }) },
    {
      method: 'GET',
      match: path('/admin/rounds/r1/flavio-overrides'),
      respond: () => ({ json: panelData() }),
    },
    {
      method: 'PUT',
      match: path('/admin/rounds/r1/flavio-overrides'),
      respond: () => {
        attempts++;
        return {
          status: 400,
          json: { message: 'Finalize as rodadas reabertas antes de recalcular.' },
        };
      },
    },
  ]);
  await page.goto('/admin/rounds/r1');
  const panel = page.locator('app-admin-flavio-overrides');
  const button = panel.getByRole('button', { name: 'Dispensar regra com justificativa' });
  const modal = page.locator('.modal');
  await button.click();
  await modal.locator('.btn-outline-secondary').click();
  expect(attempts).toBe(0);
  await button.click();
  await modal.locator('textarea').fill('x'.repeat(501));
  await modal.locator('.btn-primary').click();
  await expect(page.locator('.toast-body')).toContainText('1 a 500');
  expect(attempts).toBe(0);
  await button.click();
  await modal.locator('textarea').fill('Recebido no prazo.');
  await modal.locator('.btn-primary').click();
  await expect(button).toBeEnabled();
  await expect(panel).toContainText('17 / 8');
  await expect(page.locator('.toast-body').last()).toContainText('rodadas reabertas');
  expect(attempts).toBe(1);
});

test('published round saves without a season recalculation confirmation', async ({ page }) => {
  await seedAuth(page, 'pt-BR');
  let saved = false;
  await installApi(page, [
    { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
    {
      method: 'GET',
      match: path('/admin/rounds/r1/predictions/coverage'),
      respond: () => ({
        json: {
          roundId: 'r1',
          matchCount: 2,
          totalParticipants: 1,
          completeParticipants: 1,
          missing: [],
        },
      }),
    },
    {
      method: 'GET',
      match: path('/admin/rounds/r1/flavio-overrides'),
      respond: () => ({ json: panelData() }),
    },
    {
      method: 'PUT',
      match: path('/admin/rounds/r1/flavio-overrides'),
      respond: () => {
        saved = true;
        return { status: 204 };
      },
    },
  ]);
  await page.goto('/admin/rounds/r1');
  await page.locator('app-admin-flavio-overrides').getByRole('button').click();
  const modal = page.locator('.modal');
  await expect(modal).toContainText('quando a rodada for pontuada');
  await expect(modal.locator('.btn-primary')).toHaveText('Salvar');
  await modal.locator('textarea').fill('Enviado no prazo.');
  await modal.locator('.btn-primary').click();
  await expect(page.locator('.toast-body')).toContainText('Dispensa atualizada.');
  expect(saved).toBe(true);
});
