import { expect, test } from '@playwright/test';
import { installApi, path, pngBytes, round, seedAuth } from './support';

const summaries = [
  {
    id: 'b1',
    roundId: 'r1',
    status: 'Confirmed',
    originalFileName: 'palpites-joao.png',
    languageUsed: 'por',
    hasImage: true,
    imageContentType: 'image/png',
    imageByteSize: 2 * 1024 * 1024,
    candidateCount: 8,
    uploadedByUserId: 'u1',
    uploadedByName: 'Admin Palpitão',
    createdAt: '2026-01-02T10:00:00Z',
    processedAt: '2026-01-02T10:00:05Z',
    confirmedAt: '2026-01-02T10:02:00Z',
  },
  {
    id: 'b2',
    roundId: 'r1',
    status: 'Failed',
    originalFileName: 'borrada.png',
    languageUsed: 'por',
    // Retention pruned the bytes; the row itself survives.
    hasImage: false,
    imageContentType: null,
    imageByteSize: null,
    candidateCount: 0,
    uploadedByUserId: 'u1',
    uploadedByName: 'Admin Palpitão',
    createdAt: '2026-01-01T09:00:00Z',
    processedAt: null,
    confirmedAt: null,
  },
];

test.describe('Admin OCR import history', () => {
  test('lists past imports and opens a stored image in the viewer', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    let imageRequests = 0;
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/ocr-imports'),
        respond: () => ({ json: summaries }),
      },
      {
        method: 'GET',
        match: path('/admin/ocr-imports/b1/image'),
        respond: () => {
          imageRequests += 1;
          return { body: pngBytes, contentType: 'image/png' };
        },
      },
    ]);

    await page.goto('/admin/rounds/r1/import-history');
    await expect(page.getByRole('heading', { name: 'Histórico de importações' })).toBeVisible();

    // Both cards render, newest first.
    await expect(page.getByText('palpites-joao.png')).toBeVisible();
    await expect(page.getByText('borrada.png')).toBeVisible();
    await expect(page.getByText('Confirmada')).toBeVisible();
    await expect(page.getByText('2.0 MB')).toBeVisible();

    // Nothing is downloaded until an image is asked for.
    expect(imageRequests).toBe(0);

    // The pruned batch cannot be viewed.
    await expect(page.getByText('Imagem não está mais armazenada')).toBeVisible();

    await page.getByRole('button', { name: 'Ver imagem' }).first().click();

    // The lightbox opens with the fetched bytes as an object URL.
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('img')).toHaveAttribute('src', /^blob:/);
    expect(imageRequests).toBe(1);

    // ESC closes it.
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });

  test('shows a discarded import as cancelled, not as a failure', async ({ page }) => {
    // A batch the admin discarded used to be stored (and shown) as Failed, which read as if
    // the OCR had broken on an image that was simply re-sent.
    await seedAuth(page, 'pt-BR');
    const cancelled = [
      { ...summaries[0], id: 'b3', status: 'Cancelled', originalFileName: 'descartada.png' },
    ];
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      {
        method: 'GET',
        match: path('/admin/rounds/r1/ocr-imports'),
        respond: () => ({ json: cancelled }),
      },
    ]);

    await page.goto('/admin/rounds/r1/import-history');

    await expect(page.getByText('descartada.png')).toBeVisible();
    await expect(page.getByText('Cancelada')).toBeVisible();
    await expect(page.getByText('Falhou')).toBeHidden();
    // A cancelled batch cannot be reopened for review.
    await expect(page.getByRole('link', { name: 'Revisar' })).toBeHidden();
  });

  test('shows an empty state when the round has no imports', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/rounds/r1'), respond: () => ({ json: round }) },
      { method: 'GET', match: path('/admin/rounds/r1/ocr-imports'), respond: () => ({ json: [] }) },
    ]);

    await page.goto('/admin/rounds/r1/import-history');

    await expect(page.getByText('Nenhuma imagem foi importada nesta rodada ainda.')).toBeVisible();
  });
});
