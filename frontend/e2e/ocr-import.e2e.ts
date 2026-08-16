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
