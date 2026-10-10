import { describe, expect, it } from 'vitest';
import { Competition, MatchPhase, TournamentType } from '@core/models/enums';
import {
  ENGLAND_COMPETITIONS,
  ENGLAND_PHASES,
  WORLD_CUP_FLAVIO_PHASES,
  WORLD_CUP_PHASES,
  competitionsForType,
  phasesForType,
} from './tournament-rules.util';

describe('tournament rules', () => {
  it('offers the four English competitions to an England season', () => {
    expect(competitionsForType(TournamentType.PalpitaoEngland)).toEqual(ENGLAND_COMPETITIONS);
    expect(ENGLAND_COMPETITIONS).not.toContain(Competition.FifaWorldCup);
  });

  it('offers only the World Cup to a World Cup season', () => {
    expect(competitionsForType(TournamentType.FifaWorldCup)).toEqual([Competition.FifaWorldCup]);
    expect(phasesForType(TournamentType.FifaWorldCup)).toEqual(WORLD_CUP_PHASES);
  });

  it('falls back to the England rules when the type is unknown', () => {
    expect(competitionsForType(null)).toEqual(ENGLAND_COMPETITIONS);
    expect(phasesForType(undefined)).toEqual(ENGLAND_PHASES);
  });

  it('applies the World Cup Flávio Rule only from the quarter-finals', () => {
    expect(WORLD_CUP_FLAVIO_PHASES.every((phase) => WORLD_CUP_PHASES.includes(phase))).toBe(true);
    expect(WORLD_CUP_FLAVIO_PHASES).not.toContain(MatchPhase.WorldCupRoundOf16);
    expect(WORLD_CUP_FLAVIO_PHASES).toContain(MatchPhase.WorldCupQuarterFinal);
  });
});
