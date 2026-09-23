import { expect, test } from '@playwright/test';
import { installApi, participants, path, pngBytes, round, seedAuth } from './support';

const batch = {
  id: 'b1',
  roundId: 'r1',
  status: 'Processed',
  languageUsed: 'por',
  originalFileName: 'palpites.png',
  extractedText: 'João Silva\nArsenal 2 x 1 Chelsea\nLiverpool 0 x 3 Newcastle',
  // The just-picked file supplies the preview, so no image is fetched back here.
  hasImage: true,
  createdAt: '2026-01-01T00:00:00Z',
  processedAt: '2026-01-01T00:00:00Z',
  confirmedAt: null,
  candidates: [
    {
      id: 'c1',
      userId: 'p1',
      participantNameRaw: 'João Silva',
      roundMatchId: 'm1',
      matchTextRaw: 'Arsenal 2 x 1 Chelsea',
      predictedHomeScore: 2,
      predictedAwayScore: 1,
      confidence: 1,
      needsReview: false,
      reviewNotes: null,
    },
    {
      id: 'c2',
      userId: 'p1',
      participantNameRaw: 'João Silva',
      roundMatchId: 'm2',
      matchTextRaw: 'Liverpool 0 x 3 Newcastle',
      predictedHomeScore: 0,
      predictedAwayScore: 3,
      confidence: 1,
      needsReview: false,
      reviewNotes: null,
    },
  ],
};

test.describe('Admin OCR import', () => {
  test('processes an image, lists candidates and confirms the import', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    let confirmed = false;
    let importLanguage = '';
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/import-image'),
        respond: (req) => {
          importLanguage = (req.postData() ?? '').includes('por') ? 'por' : '';
          return { json: batch };
        },
      },
      {
        method: 'POST',
        match: path('/admin/ocr-imports/b1/confirm'),
        respond: () => {
          confirmed = true;
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/import-predictions');
    await expect(page.getByText('Importar palpites por imagem')).toBeVisible();

    // Upload + process.
    await page
      .locator('input[type="file"]')
      .setInputFiles({ name: 'palpites.png', mimeType: 'image/png', buffer: pngBytes });
    await page.getByRole('button', { name: 'Processar imagem' }).click();

    // Extracted text and candidate list render.
    await expect(page.getByText('Texto extraído')).toBeVisible();
    await expect(page.locator('pre')).toContainText('Arsenal 2 x 1 Chelsea');
    await expect(page.getByText(/Candidatos de palpite/)).toBeVisible();
    await expect(page.getByText('Arsenal 2 x 1 Chelsea · João Silva')).toBeVisible();
    expect(importLanguage).toBe('por');

    // Confirm.
    await page.getByRole('button', { name: 'Confirmar importação' }).click();
    await expect(page.locator('.toast-body')).toHaveText('Importação confirmada!');
    expect(confirmed).toBe(true);
  });

  test('files every candidate against one participant from the batch selector', async ({
    page,
  }) => {
    // What the admin actually needs when OCR misreads the name on a WhatsApp screenshot:
    // pick the person once instead of on all twelve cards.
    await seedAuth(page, 'pt-BR');

    const unresolved = {
      ...batch,
      candidates: batch.candidates.map((c) => ({
        ...c,
        userId: null,
        participantNameRaw: 'nAc',
        confidence: 0.5,
        needsReview: true,
      })),
    };
    const saved: string[] = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/import-image'),
        respond: () => ({ json: unresolved }),
      },
      {
        method: 'PUT',
        match: (p) => /\/admin\/ocr-imports\/b1\/candidates\/(c1|c2)$/.test(p),
        respond: (req) => {
          saved.push(req.url().split('/').pop()!);
          return { json: batch };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/import-predictions');
    await page
      .locator('input[type="file"]')
      .setInputFiles({ name: 'palpites.png', mimeType: 'image/png', buffer: pngBytes });
    await page.getByRole('button', { name: 'Processar imagem' }).click();

    // The name OCR read is shown, and nothing is filed yet.
    await expect(page.getByText('nome lido: nAc')).toBeVisible();
    await expect(page.getByText('2 para revisar')).toBeVisible();

    await page.selectOption('#ocr-batch-participant', { label: 'João Silva' });

    // Both cards saved through the normal per-candidate autosave.
    await expect.poll(() => saved.slice().sort()).toEqual(['c1', 'c2']);
    await expect(page.getByRole('button', { name: 'Confirmar importação' })).toBeEnabled();
  });

  test('imports a round of screenshots at once and confirms the ready ones', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    // Each file is filed under the participant it is named after; Maria's has a flagged row.
    const imported = (id: string, file: string, userId: string, flagged: boolean) => ({
      ...batch,
      id,
      originalFileName: file,
      candidates: batch.candidates.map((c, i) => ({
        ...c,
        id: `${id}-c${i}`,
        userId,
        needsReview: flagged && i === 0,
        reviewNotes:
          flagged && i === 0 ? 'O OCR leu este placar de formas diferentes (1x1 / 0x1).' : null,
      })),
    });
    const summaries: unknown[] = [];
    const confirmed: string[] = [];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/ocr-imports'),
        respond: () => ({ json: summaries }),
      },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/import-image'),
        respond: (req) => {
          const maria = (req.postData() ?? '').includes('Maria.png');
          const b = maria
            ? imported('b2', 'Maria.png', 'p2', true)
            : imported('b1', 'Joao.png', 'p1', false);
          summaries.push({
            id: b.id,
            roundId: 'r1',
            status: 'Processed',
            originalFileName: b.originalFileName,
            languageUsed: 'por',
            hasImage: true,
            candidateCount: 2,
            needsReviewCount: maria ? 1 : 0,
            participantUserId: maria ? 'p2' : 'p1',
            uploadedByUserId: 'u1',
            createdAt: '2026-01-01T00:00:00Z',
          });
          return { json: b };
        },
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/ocr-imports\/b\d\/confirm$/.test(p),
        respond: (req) => {
          confirmed.push(req.url().split('/').at(-2)!);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/import-predictions');
    await page.locator('input[type="file"]').setInputFiles([
      { name: 'Joao.png', mimeType: 'image/png', buffer: pngBytes },
      { name: 'Maria.png', mimeType: 'image/png', buffer: pngBytes },
    ]);
    await page.getByRole('button', { name: 'Processar 2 imagens' }).click();

    // Both land in the pending list, each under its participant.
    await expect(page.getByText('Importações pendentes desta rodada (2)')).toBeVisible();
    await expect(page.getByText('João Silva · 2 linhas')).toBeVisible();
    await expect(page.getByText('Maria Souza · 2 linhas')).toBeVisible();

    // Only João's is ready; Maria's has a row to look at first.
    await page.getByRole('button', { name: 'Confirmar prontas (1)' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Confirmar importação' }).click();
    await expect.poll(() => confirmed).toEqual(['b1']);
  });

  test('says why a complete row still needs a look, and which lines were left out', async ({
    page,
  }) => {
    await seedAuth(page, 'pt-BR');
    const doubtful = {
      ...batch,
      ignoredLineCount: 11,
      ignoredRoundNumbers: [9],
      candidates: [
        {
          ...batch.candidates[0],
          needsReview: true,
          reviewNotes: 'O OCR leu este placar de formas diferentes (2x1 / 2x7). Confira no print.',
        },
        batch.candidates[1],
      ],
    };
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'POST',
        match: path('/admin/rounds/r1/predictions/import-image'),
        respond: () => ({ json: doubtful }),
      },
    ]);

    await page.goto('/admin/rounds/r1/import-predictions');
    await page
      .locator('input[type="file"]')
      .setInputFiles({ name: 'Ezau.png', mimeType: 'image/png', buffer: pngBytes });
    await page.getByRole('button', { name: 'Processar imagem' }).click();

    await expect(
      page.getByText('11 linhas de jogos de outra rodada (rodada 9) foram deixadas de fora.'),
    ).toBeVisible();
    await expect(
      page.getByText('Revisar: O OCR leu este placar de formas diferentes (2x1 / 2x7).', {
        exact: false,
      }),
    ).toBeVisible();
  });

  test('shows an error toast when no file was selected', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
    ]);

    await page.goto('/admin/rounds/r1/import-predictions');
    await expect(page.getByText('Importar palpites por imagem')).toBeVisible();

    // Process button is disabled until a file is chosen.
    await expect(page.getByRole('button', { name: 'Processar imagem' })).toBeDisabled();
  });
});
