import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

interface Pending {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  status: string;
}

function pendingList(): Pending[] {
  return [
    {
      id: 'u1',
      name: 'João Silva',
      email: 'joao@x.com',
      createdAt: '2026-06-10T12:00:00Z',
      status: 'PendingApproval',
    },
    {
      id: 'u2',
      name: 'Maria Souza',
      email: 'maria@x.com',
      createdAt: '2026-06-11T09:30:00Z',
      status: 'PendingApproval',
    },
  ];
}

test.describe('Admin registration requests', () => {
  test('lists pending requests and approves one', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let pending = pendingList();
    const approved: string[] = [];

    await installApi(page, [
      {
        method: 'GET',
        match: path('/admin/registration-requests'),
        respond: () => ({ json: pending }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/registration-requests\/.+\/approve$/.test(p),
        respond: (req) => {
          const id = req.url().split('/').slice(-2)[0];
          approved.push(id);
          pending = pending.filter((u) => u.id !== id);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/registration-requests');
    await expect(page.getByText('João Silva')).toBeVisible();
    await expect(page.getByText('maria@x.com')).toBeVisible();

    // Approve João -> confirm dialog -> confirm.
    await page
      .locator('.card', { hasText: 'João Silva' })
      .getByRole('button', { name: /Aprovar/ })
      .click();
    await expect(page.locator('.modal')).toBeVisible();
    await page.locator('.modal .btn-primary').click();

    await expect(page.locator('.toast-body')).toHaveText('Cadastro aprovado!');
    expect(approved).toEqual(['u1']);
    // The list refreshed without João.
    await expect(page.getByText('João Silva')).toHaveCount(0);
    await expect(page.getByText('Maria Souza')).toBeVisible();
  });

  test('rejects a request with an optional reason', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let pending = pendingList();
    const rejected: Array<{ id: string; reason?: string }> = [];

    await installApi(page, [
      {
        method: 'GET',
        match: path('/admin/registration-requests'),
        respond: () => ({ json: pending }),
      },
      {
        method: 'POST',
        match: (p) => /\/admin\/registration-requests\/.+\/reject$/.test(p),
        respond: (req) => {
          const id = req.url().split('/').slice(-2)[0];
          rejected.push({ id, reason: req.postDataJSON()?.reason });
          pending = pending.filter((u) => u.id !== id);
          return { status: 204 };
        },
      },
    ]);

    await page.goto('/admin/registration-requests');
    const card = page.locator('.card', { hasText: 'Maria Souza' });
    await card.getByRole('button', { name: /Rejeitar/ }).click();

    await card.locator('input').fill('Não faz parte do grupo.');
    await card.getByRole('button', { name: 'Confirmar rejeição' }).click();

    await expect(page.locator('.toast-body')).toHaveText('Cadastro rejeitado.');
    expect(rejected).toEqual([{ id: 'u2', reason: 'Não faz parte do grupo.' }]);
    await expect(page.getByText('Maria Souza')).toHaveCount(0);
  });
});
