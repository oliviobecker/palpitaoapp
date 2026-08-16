import { expect, test } from '@playwright/test';
import { installApi, participants, path, seedAuth } from './support';

interface AliasRow {
  id: string;
  alias: string;
  aliasRaw: string;
  userId: string;
  userName: string;
  createdAt: string;
  updatedAt: string;
}

function aliases(): AliasRow[] {
  return [
    {
      id: 'a1',
      alias: 'nac',
      aliasRaw: 'nAc',
      userId: 'p1',
      userName: 'João Silva',
      createdAt: '2026-08-14T12:30:00Z',
      updatedAt: '2026-08-14T12:30:00Z',
    },
    {
      id: 'a2',
      alias: 'paraguaio',
      aliasRaw: 'Paraguaio',
      userId: 'p2',
      userName: 'Maria Souza',
      createdAt: '2026-08-14T12:31:00Z',
      updatedAt: '2026-08-14T12:31:00Z',
    },
  ];
}

test.describe('Admin OCR aliases', () => {
  test('lists the learned aliases and re-points one at another participant', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const puts: { url: string; body: unknown }[] = [];
    await installApi(page, [
      { method: 'GET', match: path('/admin/ocr-aliases'), respond: () => ({ json: aliases() }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'PUT',
        match: (p) => /^\/admin\/ocr-aliases\/.+$/.test(p),
        respond: (req) => {
          puts.push({ url: req.url(), body: req.postDataJSON() });
          return { json: { ...aliases()[0], userId: 'p2', userName: 'Maria Souza' } };
        },
      },
    ]);

    await page.goto('/admin/ocr-aliases');
    await expect(page.getByRole('heading', { name: 'Apelidos do OCR' })).toBeVisible();
    await expect(page.getByText('nAc')).toBeVisible();
    await expect(page.getByText('Paraguaio')).toBeVisible();

    // Re-point the junk alias at the other participant.
    await page.locator('.oa-row').first().locator('select').selectOption('p2');

    await expect(page.locator('.toast-body')).toHaveText('"nAc" agora aponta para Maria Souza.');
    expect(puts).toHaveLength(1);
    expect(puts[0].url).toContain('/admin/ocr-aliases/a1');
    expect(puts[0].body).toEqual({ userId: 'p2' });
  });

  test('teaches a new alias by hand', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    const posted: unknown[] = [];
    await installApi(page, [
      { method: 'GET', match: path('/admin/ocr-aliases'), respond: () => ({ json: [] }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'POST',
        match: path('/admin/ocr-aliases'),
        respond: (req) => {
          posted.push(req.postDataJSON());
          return {
            json: {
              id: 'a3',
              alias: 'dourado',
              aliasRaw: 'Dourado',
              userId: 'p1',
              userName: 'João Silva',
              createdAt: '2026-08-15T10:00:00Z',
              updatedAt: '2026-08-15T10:00:00Z',
            },
          };
        },
      },
    ]);

    await page.goto('/admin/ocr-aliases');
    // Nothing learned yet: the empty state explains where aliases come from.
    await expect(page.getByText(/Nenhum apelido ainda/)).toBeVisible();

    await page.locator('#alias-raw').fill('Dourado');
    await page.locator('#alias-user').selectOption({ label: 'João Silva' });
    await page.getByRole('button', { name: 'Cadastrar' }).click();

    await expect(page.locator('.toast-body')).toHaveText('"Dourado" agora aponta para João Silva.');
    expect(posted).toEqual([{ aliasRaw: 'Dourado', userId: 'p1' }]);
    // The new row joins the list without a reload, and the form is cleared for the next one.
    await expect(page.locator('.oa-row__name')).toContainText('Dourado');
    await expect(page.locator('#alias-raw')).toHaveValue('');
  });

  test('deletes an alias after confirmation', async ({ page }) => {
    await seedAuth(page, 'pt-BR');

    let deleted = '';
    await installApi(page, [
      { method: 'GET', match: path('/admin/ocr-aliases'), respond: () => ({ json: aliases() }) },
      { method: 'GET', match: path('/admin/users'), respond: () => ({ json: participants }) },
      {
        method: 'DELETE',
        match: (p) => /^\/admin\/ocr-aliases\/.+$/.test(p),
        respond: (req) => {
          deleted = req.url();
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/ocr-aliases');
    await page.locator('.oa-row').first().getByRole('button', { name: 'Excluir' }).click();

    // The dialog spells out the consequence before anything is lost.
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('nAc');
    await dialog.getByRole('button', { name: 'Excluir' }).click();

    await expect(page.locator('.toast-body')).toHaveText('Apelido excluído.');
    expect(deleted).toContain('/admin/ocr-aliases/a1');
    await expect(page.getByText('nAc')).toBeHidden();
  });
});
