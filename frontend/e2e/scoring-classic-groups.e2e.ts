import { expect, test } from '@playwright/test';
import { installApi, path, seedAuth } from './support';

const season = {
  id: 's1',
  name: 'Palpitão England 2026/2027',
  startDate: '2026-08-01T00:00:00Z',
  endDate: '2027-05-31T00:00:00Z',
  isActive: true,
  tournamentType: 'PalpitaoEngland',
  faCupEnabled: true,
  allowParticipantsToSubmitPredictions: true,
  allowParticipantsToViewOthersPredictions: false,
};

/** Arsenal + Chelsea in the Premier League group, Millwall + West Ham in the Championship one. */
const scoringConfig = {
  seasonId: 's1',
  seasonName: season.name,
  tournamentType: 'PalpitaoEngland',
  hasScoredRounds: false,
  basePoints: { columnOnly: 1, traditional: 3, medium: 5, uncommon: 7, extraUncommon: 10 },
  rules: {
    flavioFromRound: 16,
    absenceFromRound: 1,
    absencePenaltyPoints: 20,
    absenceEliminationCount: 5,
  },
  scoreEntries: [],
  multiplierRules: [
    { competition: 'PremierLeague', phase: 'Regular', multiplier: 1, classicMultiplier: 2 },
    { competition: 'Championship', phase: 'Regular', multiplier: 1, classicMultiplier: 2 },
  ],
  teams: [
    {
      teamId: 't1',
      name: 'Arsenal',
      shortName: 'ARS',
      isClassic: true,
      classicCompetition: 'PremierLeague',
    },
    {
      teamId: 't2',
      name: 'Chelsea',
      shortName: 'CHE',
      isClassic: true,
      classicCompetition: 'PremierLeague',
    },
    {
      teamId: 't3',
      name: 'Millwall',
      shortName: 'MIL',
      isClassic: true,
      classicCompetition: 'Championship',
    },
    {
      teamId: 't4',
      name: 'West Ham United',
      shortName: 'WHU',
      isClassic: true,
      classicCompetition: 'Championship',
    },
    {
      teamId: 't5',
      name: 'Norwich City',
      shortName: 'NOR',
      isClassic: false,
      classicCompetition: null,
    },
  ],
};

test.describe('Classic team groups', () => {
  test('shows one group per competition and saves a team into the picked one', async ({ page }) => {
    let saved: { classicTeams?: { teamId: string; competition: string }[] } = {};
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: [season] }) },
      {
        method: 'GET',
        match: path('/seasons/s1/scoring-config'),
        respond: () => ({ json: scoringConfig }),
      },
      {
        method: 'PUT',
        match: path('/seasons/s1/scoring-config'),
        respond: (req) => {
          saved = req.postDataJSON();
          return { json: scoringConfig };
        },
      },
    ]);

    await page.goto('/admin/scoring');

    // The two England groups are rendered separately, each with its own chips.
    const groups = page.locator('.classic-group');
    await expect(groups).toHaveCount(2);
    await expect(groups.first()).toContainText('Arsenal');
    await expect(groups.first()).toContainText('Chelsea');
    await expect(groups.last()).toContainText('Millwall');
    await expect(groups.last()).toContainText('West Ham United');

    // Adding an unselected club to the Championship group tags it with that competition.
    await page.getByPlaceholder('Buscar time para adicionar...').fill('Norwich');
    await groups.last().locator('.add-btn').first().click();
    await page.getByRole('button', { name: 'Salvar' }).click();

    // The four pre-selected classics plus Norwich City.
    await expect.poll(() => saved.classicTeams?.length).toBe(5);
    expect(saved.classicTeams).toContainEqual({ teamId: 't5', competition: 'Championship' });
    expect(saved.classicTeams).toContainEqual({ teamId: 't1', competition: 'PremierLeague' });
  });
});

test.describe('Scoring rules season picker', () => {
  test('shows the active season it edits even when another season is listed first', async ({
    page,
  }) => {
    // Seasons come newest first, so a test season can head the list. The picker used to show
    // that first season while the page loaded (and would recalculate) the active one.
    const testSeason = {
      ...season,
      id: 's0',
      name: 'TESTE - Palpitão England 26/27',
      startDate: '2026-09-01T00:00:00Z',
      isActive: false,
    };
    const configRequests: string[] = [];
    await seedAuth(page, 'pt-BR');
    await installApi(page, [
      { method: 'GET', match: path('/seasons'), respond: () => ({ json: [testSeason, season] }) },
      {
        method: 'GET',
        match: (p) => p.endsWith('/scoring-config'),
        respond: (req) => {
          configRequests.push(new URL(req.url()).pathname);
          return { json: scoringConfig };
        },
      },
    ]);

    await page.goto('/admin/scoring');

    const picker = page.getByRole('combobox', { name: 'Temporada' });
    await expect(picker).toHaveValue('s1');
    await expect(picker.locator('option:checked')).toHaveText(/Palpitão England 2026\/2027/);
    // ...and it is the season whose rules are on screen.
    await expect(page.locator('.classic-group')).toHaveCount(2);
    expect(configRequests).toEqual(['/seasons/s1/scoring-config']);
  });
});
