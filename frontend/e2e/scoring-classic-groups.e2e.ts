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
