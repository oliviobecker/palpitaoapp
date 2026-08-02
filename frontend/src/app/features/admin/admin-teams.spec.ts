import { describe, expect, it } from 'vitest';
import { Competition, TeamType } from '../../core/models/enums';
import { Team, TeamSyncResponse } from '../../core/models/models';
import { diffTotals, filterTeams, groupTeams } from './admin-teams';

function team(partial: Partial<Team> & { name: string }): Team {
  return {
    id: partial.id ?? partial.name,
    name: partial.name,
    shortName: partial.shortName ?? partial.name.slice(0, 3).toUpperCase(),
    isBigSevenClub: partial.isBigSevenClub ?? false,
    division: partial.division ?? null,
    teamType: partial.teamType,
  };
}

describe('groupTeams', () => {
  it('lists the three divisions in order, even when one is empty', () => {
    const groups = groupTeams([team({ name: 'Arsenal', division: Competition.PremierLeague })]);

    expect(groups.map((g) => g.key)).toEqual([
      Competition.PremierLeague,
      Competition.Championship,
      Competition.LeagueOne,
    ]);
    // An empty division has to stay visible — that is the gap the admin is reviewing.
    expect(groups[1].teams).toEqual([]);
    expect(groups[1].expected).toBe(24);
  });

  it('appends the unassigned and national groups, and only when they have teams', () => {
    const groups = groupTeams([
      team({ name: 'Port Vale' }),
      team({ name: 'Brazil', teamType: TeamType.NationalTeam }),
    ]);

    expect(groups.map((g) => g.key).slice(3)).toEqual(['unassigned', 'national']);
    expect(groups[3].teams.map((t) => t.name)).toEqual(['Port Vale']);
    expect(groups[4].teams.map((t) => t.name)).toEqual(['Brazil']);
    expect(groups[3].expected).toBeNull();
  });

  it('omits the unassigned and national groups when empty', () => {
    const groups = groupTeams([team({ name: 'Arsenal', division: Competition.PremierLeague })]);
    expect(groups).toHaveLength(3);
  });

  it('sorts each group by name', () => {
    const groups = groupTeams([
      team({ name: 'Wolves', division: Competition.Championship }),
      team({ name: 'Blackburn', division: Competition.Championship }),
      team({ name: 'Millwall', division: Competition.Championship }),
    ]);

    expect(groups[1].teams.map((t) => t.name)).toEqual(['Blackburn', 'Millwall', 'Wolves']);
  });

  it('treats a team without teamType as a club', () => {
    // Payloads predating the field must not land in the national-team group.
    const groups = groupTeams([team({ name: 'Exeter City' })]);

    expect(groups.map((g) => g.key)).toContain('unassigned');
    expect(groups.map((g) => g.key)).not.toContain('national');
  });

  it('keeps a national team out of the divisions even if it carries one', () => {
    const groups = groupTeams([
      team({
        name: 'England',
        teamType: TeamType.NationalTeam,
        division: Competition.PremierLeague,
      }),
    ]);

    expect(groups[0].teams).toEqual([]);
    expect(groups.at(-1)?.key).toBe('national');
  });
});

describe('filterTeams', () => {
  const teams = [
    team({ name: 'Arsenal', shortName: 'ARS' }),
    team({ name: 'Chelsea', shortName: 'CHE' }),
  ];

  it('returns everything for an empty query', () => {
    expect(filterTeams(teams, '   ')).toHaveLength(2);
  });

  it('matches the name case-insensitively', () => {
    expect(filterTeams(teams, 'aRsE').map((t) => t.name)).toEqual(['Arsenal']);
  });

  it('matches the short name', () => {
    expect(filterTeams(teams, 'che').map((t) => t.name)).toEqual(['Chelsea']);
  });
});

describe('diffTotals', () => {
  const sync: TeamSyncResponse = {
    source: 'OneFootball',
    applied: false,
    competitions: [
      {
        competition: Competition.PremierLeague,
        foundCount: 20,
        toCreate: [{ name: 'New FC', shortName: 'NEW', isBigSevenClub: false }],
        toMove: [
          {
            teamId: 't1',
            name: 'Leeds',
            fromDivision: Competition.Championship,
            toDivision: Competition.PremierLeague,
          },
        ],
        notFound: [{ teamId: 't2', name: 'Old FC' }],
        unchangedCount: 18,
      },
      {
        competition: Competition.Championship,
        foundCount: 24,
        toCreate: [],
        toMove: [],
        notFound: [],
        unchangedCount: 23,
      },
    ],
  };

  it('sums each bucket across competitions', () => {
    expect(diffTotals(sync)).toEqual({
      created: 1,
      moved: 1,
      notFound: 1,
      unchanged: 41,
      found: 44,
    });
  });

  it('reports zeros when nothing changes', () => {
    const totals = diffTotals({ ...sync, competitions: [sync.competitions[1]] });
    expect(totals.created + totals.moved).toBe(0);
  });
});
