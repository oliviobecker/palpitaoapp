import { Competition, TeamType } from '@core/models/enums';
import { Team, TeamSyncResponse } from '@core/models';

/** The league divisions a club can be moved to; `null` means "no division". */
export const TEAM_DIVISIONS: Competition[] = [
  Competition.PremierLeague,
  Competition.Championship,
  Competition.LeagueOne,
];

/** Roster size each division is expected to have, shown next to the count as a review hint. */
export const EXPECTED_ROSTER: Partial<Record<Competition, number>> = {
  [Competition.PremierLeague]: 20,
  [Competition.Championship]: 24,
  [Competition.LeagueOne]: 24,
};

export type TeamGroupKey = Competition | 'unassigned' | 'national';

export interface TeamGroup {
  key: TeamGroupKey;
  /** Set for the league groups; null for the "no division" and national-team groups. */
  competition: Competition | null;
  teams: Team[];
  expected: number | null;
}

export function filterTeams(teams: Team[], query: string): Team[] {
  const term = query.trim().toLowerCase();
  if (!term) {
    return teams;
  }

  return teams.filter(
    (t) => t.name.toLowerCase().includes(term) || (t.shortName ?? '').toLowerCase().includes(term),
  );
}

/**
 * Groups the catalogue for review: the three divisions first (always shown, so an
 * empty one is visible rather than absent), then the clubs with no division, then
 * the national teams. A payload without `teamType` predates the field and is read
 * as a club.
 */
export function groupTeams(teams: Team[]): TeamGroup[] {
  const byName = (a: Team, b: Team) => a.name.localeCompare(b.name);
  const isNational = (t: Team) => t.teamType === TeamType.NationalTeam;

  const groups: TeamGroup[] = TEAM_DIVISIONS.map((competition) => ({
    key: competition,
    competition,
    teams: teams.filter((t) => !isNational(t) && t.division === competition).sort(byName),
    expected: EXPECTED_ROSTER[competition] ?? null,
  }));

  const unassigned = teams.filter((t) => !isNational(t) && !t.division).sort(byName);
  if (unassigned.length > 0) {
    groups.push({ key: 'unassigned', competition: null, teams: unassigned, expected: null });
  }

  const national = teams.filter(isNational).sort(byName);
  if (national.length > 0) {
    groups.push({ key: 'national', competition: null, teams: national, expected: null });
  }

  return groups;
}

export interface DiffTotals {
  created: number;
  moved: number;
  notFound: number;
  unchanged: number;
  found: number;
}

export function diffTotals(sync: TeamSyncResponse): DiffTotals {
  const sum = (pick: (c: TeamSyncResponse['competitions'][number]) => number) =>
    sync.competitions.reduce((acc, c) => acc + pick(c), 0);

  return {
    created: sum((c) => c.toCreate.length),
    moved: sum((c) => c.toMove.length),
    notFound: sum((c) => c.notFound.length),
    unchanged: sum((c) => c.unchangedCount),
    found: sum((c) => c.foundCount),
  };
}
