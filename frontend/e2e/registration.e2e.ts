import { expect, test } from '@playwright/test';
import { installApi, path } from './support';

const PENDING_MESSAGE =
  'Cadastro enviado com sucesso. Aguarde a aprovação do administrador para acessar o sistema.';

test.describe('Public registration', () => {
  test('validates required fields and password confirmation', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));
    await page.goto('/register');
    await expect(page.getByText('Crie sua conta')).toBeVisible();

    // Submit empty -> required error.
    await page.getByRole('button', { name: 'Cadastrar' }).click();
    await expect(page.getByText('Informe seu nome.')).toBeVisible();

    // Mismatched confirmation -> form-level error.
    await page.fill('#name', 'João Silva');
    await page.fill('#email', 'joao@x.com');
    await page.fill('#password', 'Senha123');
    await page.fill('#confirmPassword', 'Outra123');
    await page.getByRole('button', { name: 'Cadastrar' }).click();
    await expect(page.getByText('As senhas não conferem.')).toBeVisible();
  });

  test('submits a valid registration and shows the pending-approval message', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));

    const submitted: Array<Record<string, unknown>> = [];
    await installApi(page, [
      {
        method: 'GET',
        match: path('/public/groups'),
        respond: () => ({
          json: [{ id: 'g1', name: 'Grupo Teste', slug: 'grupo-teste', description: null }],
        }),
      },
      {
        method: 'POST',
        match: path('/auth/register'),
        respond: (req) => {
          submitted.push(req.postDataJSON());
          return { json: { message: PENDING_MESSAGE } };
        },
      },
    ]);

    await page.goto('/register');
    // The active groups load into the picker; choose one.
    await page.selectOption('#groupId', { label: 'Grupo Teste' });
    await page.fill('#name', 'João Silva');
    await page.fill('#email', 'joao@x.com');
    await page.fill('#password', 'Senha123');
    await page.fill('#confirmPassword', 'Senha123');
    await page.getByRole('button', { name: 'Cadastrar' }).click();

    await expect(page.getByText(PENDING_MESSAGE)).toBeVisible();
    await expect(page.getByRole('link', { name: 'Ir para o login' })).toBeVisible();

    expect(submitted).toHaveLength(1);
    expect(submitted[0]).toMatchObject({
      groupId: 'g1',
      name: 'João Silva',
      email: 'joao@x.com',
      password: 'Senha123',
      confirmPassword: 'Senha123',
    });
  });

  test('blocks login for a pending account with a friendly message', async ({ page }) => {
    await page.addInitScript(() => localStorage.setItem('palpitao.lang', 'pt-BR'));
    await installApi(page, [
      {
        method: 'POST',
        match: path('/auth/login'),
        respond: () => ({
          status: 403,
          json: { message: 'Seu cadastro ainda está pendente de aprovação.' },
        }),
      },
    ]);

    await page.goto('/login');
    await page.fill('#email', 'joao@x.com');
    await page.fill('#password', 'Senha123');
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page.getByText('Seu cadastro ainda está pendente de aprovação.')).toBeVisible();
  });
});
