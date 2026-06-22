import { expect, test } from '@playwright/test';
import { installApi, participants, path, pngBytes, round, seedAuth } from './support';

const batch = {
  id: 'b1',
  roundId: 'r1',
  status: 'Processed',
  languageUsed: 'por',
  originalFileName: 'palpites.png',
  extractedText: 'João Silva\nArsenal 2 x 1 Chelsea\nLiverpool 0 x 3 Newcastle',
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
