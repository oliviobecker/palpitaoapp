import { describe, expect, it } from 'vitest';
import { Competition, MatchPhase, TournamentType } from '../../core/models/enums';
import { RoundMatch, ScoringConfig } from '../../core/models/models';
import { computeMultiplier, isClassic } from './match.util';

function match(partial: Partial<RoundMatch>): RoundMatch {
  return {
    id: 'm',
    roundId: 'r',
    competition: Competition.FifaWorldCup,
    phase: MatchPhase.WorldCupGroupStage,
    homeTeamId: 'h',
    homeTeamName: 'Brazil',
    awayTeamId: 'a',
    awayTeamName: 'Scotland',
    startsAt: '2026-06-24T22:00:00Z',
    order: 0,
    isFinished: false,
    ...partial,
  };
}

describe('match.util — FIFA World Cup multipliers', () => {
  it('applies the phase multiplier (group x1, R32/R16 x2, quarters+ x3)', () => {
    expect(computeMultiplier(match({ phase: MatchPhase.WorldCupGroupStage }))).toBe(1);
    expect(computeMultiplier(match({ phase: MatchPhase.WorldCupRoundOf32 }))).toBe(2);
    expect(computeMultiplier(match({ phase: MatchPhase.WorldCupRoundOf16 }))).toBe(2);
    expect(computeMultiplier(match({ phase: MatchPhase.WorldCupQuarterFinal }))).toBe(3);
    expect(computeMultiplier(match({ phase: MatchPhase.WorldCupFinal }))).toBe(3);
  });

  it('doubles knockout classics between two world champions', () => {
    const champs = { homeTeamName: 'Brazil', awayTeamName: 'Germany' };
    // Group stage classics are NOT doubled.
    expect(computeMultiplier(match({ ...champs, phase: MatchPhase.WorldCupGroupStage }))).toBe(1);
    // R32 classic: x2 -> x4; quarter classic: x3 -> x6.
    expect(computeMultiplier(match({ ...champs, phase: MatchPhase.WorldCupRoundOf32 }))).toBe(4);
    expect(computeMultiplier(match({ ...champs, phase: MatchPhase.WorldCupQuarterFinal }))).toBe(6);
    // Non-champion opponent -> no doubling.
    expect(
      computeMultiplier(
        match({
          homeTeamName: 'Brazil',
          awayTeamName: 'Japan',
          phase: MatchPhase.WorldCupQuarterFinal,
        }),
      ),
    ).toBe(3);
  });

  it('flags World Cup classics only in the knockout', () => {
    const champs = { homeTeamName: 'Spain', awayTeamName: 'Uruguay' };
    expect(isClassic(match({ ...champs, phase: MatchPhase.WorldCupFinal }))).toBe(true);
    expect(isClassic(match({ ...champs, phase: MatchPhase.WorldCupGroupStage }))).toBe(false);
  });

  it('honours the manual multiplier override', () => {
    expect(
      computeMultiplier(
        match({ phase: MatchPhase.WorldCupGroupStage, manualMultiplierOverride: 5 }),
      ),
    ).toBe(5);
  });
});

describe('match.util — config-driven multipliers', () => {
  const config: ScoringConfig = {
    seasonId: 's',
    seasonName: 'Test',
    tournamentType: TournamentType.PalpitaoEngland,
    hasScoredRounds: false,
    basePoints: { columnOnly: 1, traditional: 3, medium: 5, uncommon: 7, extraUncommon: 10 },
    scoreEntries: [],
    multiplierRules: [
      // Custom: Premier League classic is x5 (vs default x2).
      {
        competition: Competition.PremierLeague,
        phase: MatchPhase.Regular,
        multiplier: 1,
        classicMultiplier: 5,
      },
    ],
    teams: [
      { teamId: 'h', name: 'Arsenal', shortName: 'ARS', isClassic: true },
      { teamId: 'a', name: 'Chelsea', shortName: 'CHE', isClassic: true },
    ],
  };

  const plMatch = (over: Partial<RoundMatch> = {}): RoundMatch =>
    match({ competition: Competition.PremierLeague, phase: MatchPhase.Regular, ...over });

  it('uses the season config table when provided', () => {
    // Both teams classic-eligible by id -> custom classic multiplier.
    expect(computeMultiplier(plMatch(), config)).toBe(5);
    expect(isClassic(plMatch(), config)).toBe(true);
  });

  it('uses the normal multiplier when a team is not classic-eligible', () => {
    expect(computeMultiplier(plMatch({ awayTeamId: 'other' }), config)).toBe(1);
    expect(isClassic(plMatch({ awayTeamId: 'other' }), config)).toBe(false);
  });

  it('falls back to defaults when no config is given', () => {
    // Default Premier League classic is x2 (Arsenal x Chelsea by name).
    expect(computeMultiplier(plMatch({ homeTeamName: 'Arsenal', awayTeamName: 'Chelsea' }))).toBe(
      2,
    );
  });
});
