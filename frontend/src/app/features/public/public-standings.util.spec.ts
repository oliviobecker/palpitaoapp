import { describe, expect, it } from 'vitest';
import { Competition, MatchPhase, RoundStatus, ScoreCategory } from '@core/models/enums';
import {
  PublicMatchScore,
  PublicParticipantScore,
  PublicRound,
  PublicRoundSummary,
  PublicStandingRow,
  RoundResultMatch,
} from '@core/models';
import {
  categoryLabelKey,
  formatRoundDates,
  hasResult,
  pivotByMatch,
  rankRows,
  resolveRound,
} from './public-standings.util';

const row = (userId: string, name: string, totalPoints: number): PublicStandingRow =>
  ({ userId, name, totalPoints, position: 0, rounds: [] }) as unknown as PublicStandingRow;

const summary = (number: number, part?: number): PublicRoundSummary => ({
  number,
  part,
  status: RoundStatus.Scored,
  isScored: true,
});

const match = (id: string, homeScore: number | null = 1, awayScore: number | null = 0) =>
  ({
    roundMatchId: id,
    competition: Competition.PremierLeague,
    phase: MatchPhase.Regular,
    homeTeamName: 'Arsenal',
    awayTeamName: 'Chelsea',
    homeScore,
    awayScore,
  }) as unknown as RoundResultMatch;

const score = (roundMatchId: string, finalPoints: number) =>
  ({ roundMatchId, finalPoints, scoreCategory: ScoreCategory.Traditional }) as PublicMatchScore;

const participant = (userId: string, name: string, scores: PublicMatchScore[]) =>
  ({ userId, name, matchScores: scores }) as unknown as PublicParticipantScore;

describe('rankRows', () => {
  const standings = [
    row('a', 'Ana', 30),
    row('b', 'Bia', 30),
    row('c', 'Caio', 22),
    row('d', 'Duda', 20),
  ];

  it('measures each row against the leader and the row above', () => {
    const ranked = rankRows(standings, '');
    expect(ranked.map((r) => [r.toLeader, r.toAbove])).toEqual([
      [0, 0],
      [0, 0],
      [8, 8],
      [10, 2],
    ]);
  });

  it('names the row above, unless it is tied with the leader', () => {
    expect(rankRows(standings, '').map((r) => r.above)).toEqual([null, null, null, 'Caio']);
  });

  it('filters by name without recomputing the gaps', () => {
    const [duda] = rankRows(standings, '  DUD ');
    expect(duda.name).toBe('Duda');
    expect([duda.toLeader, duda.toAbove, duda.above]).toEqual([10, 2, 'Caio']);
  });
});

describe('pivotByMatch', () => {
  it('lists each match with its predictions best first, ties by name, and counts the hits', () => {
    const round = {
      matches: [match('m1')],
      participants: [
        participant('u1', 'Zeca', [score('m1', 3)]),
        participant('u2', 'Ana', [score('m1', 3)]),
        participant('u3', 'Bia', [score('m1', 0)]),
        participant('u4', 'Caio', []),
      ],
    } as unknown as PublicRound;

    const [m1] = pivotByMatch(round);

    expect(m1.entries.map((e) => e.name)).toEqual(['Ana', 'Zeca', 'Bia']);
    expect(m1.hits).toBe(2);
  });

  it('is empty without a round', () => {
    expect(pivotByMatch(null)).toEqual([]);
  });
});

describe('match helpers', () => {
  it('reads a match with a missing score as not played yet', () => {
    expect(hasResult(match('m', 2, 1))).toBe(true);
    expect(hasResult(match('m', null, null))).toBe(false);
  });

  it('labels a missed prediction as missed', () => {
    expect(categoryLabelKey(ScoreCategory.Medium)).toBe('category.Medium');
    expect(categoryLabelKey(ScoreCategory.None)).toBe('publicStandings.missed');
  });
});

describe('formatRoundDates', () => {
  const dated = (startDate?: string, endDate?: string): PublicRoundSummary => ({
    ...summary(1),
    startDate,
    endDate,
  });

  it('names the month once when both ends share it', () => {
    expect(formatRoundDates(dated('2026-05-12', '2026-05-14'), 'en-US')).toBe('12–14 May');
  });

  it('names both months across a month boundary', () => {
    expect(formatRoundDates(dated('2026-04-30', '2026-05-02'), 'en-US')).toBe('30 Apr – 2 May');
  });

  it('shows a single day once, and nothing for an undated round', () => {
    expect(formatRoundDates(dated('2026-05-12', '2026-05-12'), 'en-US')).toBe('12 May');
    expect(formatRoundDates(dated(), 'en-US')).toBe('');
  });
});

describe('resolveRound', () => {
  const available = [summary(11), summary(10, 2), summary(10, 1), summary(9)];

  it('lands on the exact round asked for', () => {
    expect(resolveRound(available, { number: 10, part: 2 })).toEqual({
      round: { number: 10, part: 2 },
      missing: null,
    });
  });

  it('lands on the first part when a link predates the split', () => {
    expect(resolveRound(available, { number: 10 }).round).toEqual({ number: 10, part: 1 });
  });

  it('falls back to the newest round and says which one was missing', () => {
    expect(resolveRound(available, { number: 30 })).toEqual({
      round: { number: 11, part: 0 },
      missing: '30',
    });
  });

  it('opens the newest round when no round was asked for', () => {
    expect(resolveRound(available, null)).toEqual({
      round: { number: 11, part: 0 },
      missing: null,
    });
  });
});
