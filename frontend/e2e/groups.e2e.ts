import { expect, test } from '@playwright/test';
import { adminUser, installApi, path } from './support';

const loginResponse = {
  token: 'e2e-fake-jwt',
  expiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'e2e-fake-refresh',
  refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  user: adminUser,
};

test.describe('Multi-group login', () => {
  test('with several approved groups, shows the chooser then enters the picked one', async ({
    page,
  }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));
    const headers: string[] = [];

    await installApi(page, [
      { method: 'POST', match: path('/auth/login'), respond: () => ({ json: loginResponse }) },
      {
        method: 'GET',
        match: path('/auth/my-groups'),
        respond: () => ({
          json: [
            {
              groupId: 'g1',
              groupName: 'Palpitão England',
              slug: 'eng',
              role: 'GroupAdmin',
              status: 'Approved',
            },
            {
              groupId: 'g2',
              groupName: 'Grupo dos Amigos',
              slug: 'amigos',
              role: 'Participant',
              status: 'Approved',
            },
          ],
        }),
      },
      {
        method: 'GET',
        match: path('/rounds'),
        respond: (req) => {
          headers.push(req.headers()['x-group-id'] ?? '');
          return { json: [] };
        },
      },
    ]);

    await page.goto('/login');
    await page.fill('#email', 'admin@palpitao.local');
    await page.fill('#password', 'Senha123');
    await page.getByRole('button', { name: 'Entrar' }).click();

    // Two groups -> the chooser appears.
    await expect(page).toHaveURL(/\/select-group$/);
    await expect(page.getByText('Palpitão England')).toBeVisible();
    await expect(page.getByText('Grupo dos Amigos')).toBeVisible();

    // Pick the GroupAdmin group -> lands on the admin area.
    await page.getByText('Palpitão England').click();
    await expect(page).toHaveURL(/\/admin$/);

    // The current group header chip is visible.
    await expect(page.getByRole('button', { name: /Palpitão England/ })).toBeVisible();

    // A platform admin (role "Admin") sees the Super Admin badge in the header.
    await expect(page.getByText('Super Admin')).toBeVisible();
  });

  test('with a single approved group, enters it automatically', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));

    await installApi(page, [
      { method: 'POST', match: path('/auth/login'), respond: () => ({ json: loginResponse }) },
      {
        method: 'GET',
        match: path('/auth/my-groups'),
        respond: () => ({
          json: [
            {
              groupId: 'g2',
              groupName: 'Grupo dos Amigos',
              slug: 'amigos',
              role: 'Participant',
              status: 'Approved',
            },
          ],
        }),
      },
    ]);

    await page.goto('/login');
    await page.fill('#email', 'maria@x.com');
    await page.fill('#password', 'Senha123');
    await page.getByRole('button', { name: 'Entrar' }).click();

    // Single Participant group -> straight to the dashboard, no chooser.
    await expect(page).toHaveURL(/\/dashboard$/);
  });

  test('blocks login when the user has no approved group', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));

    await installApi(page, [
      { method: 'POST', match: path('/auth/login'), respond: () => ({ json: loginResponse }) },
      { method: 'GET', match: path('/auth/my-groups'), respond: () => ({ json: [] }) },
    ]);

    await page.goto('/login');
    await page.fill('#email', 'newcomer@x.com');
    await page.fill('#password', 'Senha123');
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page.getByText(/aguarde a aprovação do administrador do grupo/i)).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);
  });
});
