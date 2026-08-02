import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

interface TeamRow {
  id: string;
  name: string;
  shortName: string;
  isBigSevenClub: boolean;
  division: string | null;
  teamType: string;
}

function catalogue(): TeamRow[] {
  return [
    {
      id: 't1',
      name: 'Arsenal',
      shortName: 'ARS',
      isBigSevenClub: true,
      division: 'PremierLeague',
      teamType: 'Club',
    },
    {
      id: 't2',
      name: 'Millwall',
      shortName: 'MIL',
      isBigSevenClub: false,
      division: 'Championship',
      teamType: 'Club',
    },
    {
      id: 't3',
      name: 'Bolton',
      shortName: 'BOL',
      isBigSevenClub: false,
      division: 'LeagueOne',
      teamType: 'Club',
    },
    {
      id: 't4',
      name: 'Port Vale',
      shortName: 'POR',
      isBigSevenClub: false,
      division: null,
      teamType: 'Club',
    },
    {
      id: 't5',
      name: 'Brazil',
      shortName: 'BRA',
      isBigSevenClub: false,
      division: null,
      teamType: 'NationalTeam',
    },
  ];
}

const previewWithChanges = {
  source: 'OneFootball',
  applied: false,
  competitions: [
    {
      competition: 'PremierLeague',
      foundCount: 20,
      toCreate: [{ name: 'Sunderland', shortName: 'SUN', isBigSevenClub: false }],
      toMove: [
        {
          teamId: 't2',
          name: 'Millwall',
          fromDivision: 'Championship',
          toDivision: 'PremierLeague',
        },
      ],
      notFound: [{ teamId: 't1', name: 'Arsenal' }],
      unchangedCount: 18,
    },
    {
      competition: 'Championship',
      foundCount: 0,
      toCreate: [],
      toMove: [],
      notFound: [],
      unchangedCount: 0,
    },
    {
      competition: 'LeagueOne',
      foundCount: 24,
      toCreate: [],
      toMove: [],
      notFound: [],
      unchangedCount: 24,
    },
  ],
};

test.describe('Admin team catalogue', () => {
  test('groups the catalogue by division', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/teams'), respond: () => ({ json: catalogue() }) },
    ]);

    await page.goto('/admin/teams');

    await expect(page.getByText('Catálogo de times').first()).toBeVisible();
    // Scoped to the group header: "Sem divisão" is also the null option of every select.
    await expect(page.locator('.card .badge', { hasText: 'Sem divisão' })).toBeVisible();
    await expect(page.locator('.card .badge', { hasText: 'Seleções' })).toBeVisible();
    await expect(page.getByText('1/20')).toBeVisible();
    await expect(page.getByText('1/24').first()).toBeVisible();

    // Each select opens on the division the club actually has, not on the first option.
    await expect(page.getByLabel('Divisão Arsenal')).toHaveValue('PremierLeague');
    await expect(page.getByLabel('Divisão Bolton')).toHaveValue('LeagueOne');
    await expect(page.getByLabel('Divisão Port Vale')).toHaveValue('');

    // A national team has no division to pick.
    await expect(page.getByLabel('Divisão Brazil')).toHaveCount(0);
  });

  test('moves a club to another division', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    const patched: { url: string; body: unknown }[] = [];

    await installApi(page, [
      { method: 'GET', match: path('/teams'), respond: () => ({ json: catalogue() }) },
      {
        method: 'PATCH',
        match: (p) => /^\/admin\/teams\/.+$/.test(p),
        respond: (req) => {
          patched.push({ url: req.url(), body: req.postDataJSON() });
          const team = catalogue().find((t) => t.id === 't3')!;
          return { json: { ...team, division: 'Championship' } };
        },
      },
    ]);

    await page.goto('/admin/teams');
    await page.getByLabel('Divisão Bolton').selectOption('Championship');

    await expect(page.locator('.toast-body')).toHaveText('Bolton movido para Championship.');
    expect(patched).toHaveLength(1);
    expect(patched[0].url).toContain('/admin/teams/t3');
    expect(patched[0].body).toEqual({ division: 'Championship' });

    // The row regrouped: the League One group is now empty.
    await expect(page.getByText('0/24')).toBeVisible();
  });

  test('previews the sync and applies it', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    let applied = 0;
    let listed = 0;

    await installApi(page, [
      {
        method: 'GET',
        match: path('/teams'),
        respond: () => {
          listed++;
          return { json: catalogue() };
        },
      },
      {
        method: 'POST',
        match: path('/admin/teams/sync-preview'),
        respond: () => ({ json: previewWithChanges }),
      },
      {
        method: 'POST',
        match: path('/admin/teams/sync-apply'),
        respond: () => {
          applied++;
          return { json: { ...previewWithChanges, applied: true } };
        },
      },
    ]);

    await page.goto('/admin/teams');
    await expect(page.getByText('Arsenal')).toBeVisible();
    await page.getByRole('button', { name: /Sincronizar do OneFootball/ }).click();

    await expect(page.getByText('Prévia da sincronização')).toBeVisible();
    await expect(page.getByText('Novos clubes')).toBeVisible();
    await expect(page.getByText('Sunderland')).toBeVisible();
    await expect(page.getByText('Mudanças de divisão')).toBeVisible();
    await expect(page.getByText('Ausentes na fonte')).toBeVisible();
    await expect(page.getByText(/nada é removido automaticamente/)).toBeVisible();
    // The competition with no data says so instead of reporting its clubs missing.
    await expect(page.getByText(/fora de temporada/)).toBeVisible();

    await page.getByRole('button', { name: 'Aplicar mudanças' }).click();

    await expect(page.locator('.toast-body')).toHaveText(
      'Catálogo atualizado: 1 criados, 1 movidos.',
    );
    expect(applied).toBe(1);
    await expect(page.getByText('Prévia da sincronização')).toHaveCount(0);
    // The list reloads so the applied changes show up.
    await expect.poll(() => listed).toBe(2);
  });

  test('says the catalogue is up to date when there is nothing to apply', async ({ page }) => {
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/teams'), respond: () => ({ json: catalogue() }) },
      {
        method: 'POST',
        match: path('/admin/teams/sync-preview'),
        respond: () => ({
          json: {
            source: 'OneFootball',
            applied: false,
            competitions: [
              {
                competition: 'PremierLeague',
                foundCount: 20,
                toCreate: [],
                toMove: [],
                notFound: [],
                unchangedCount: 20,
              },
            ],
          },
        }),
      },
    ]);

    await page.goto('/admin/teams');
    await page.getByRole('button', { name: /Sincronizar do OneFootball/ }).click();

    await expect(page.getByText('Tudo atualizado.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Aplicar mudanças' })).toBeDisabled();

    await page.getByRole('button', { name: 'Cancelar' }).click();
    await expect(page.getByText('Prévia da sincronização')).toHaveCount(0);
  });
});
