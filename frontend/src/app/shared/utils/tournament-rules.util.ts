import { Competition, MatchPhase, TournamentType } from '@core/models/enums';

/** Competitions/phases available to each tournament type (mirrors backend TournamentRules). */
export const ENGLAND_COMPETITIONS: Competition[] = [
  Competition.PremierLeague,
  Competition.FACup,
  Competition.Championship,
  Competition.LeagueOne,
];
export const WORLD_CUP_COMPETITIONS: Competition[] = [Competition.FifaWorldCup];

export const ENGLAND_PHASES: MatchPhase[] = [
  MatchPhase.Regular,
  MatchPhase.PlayoffSemiFinal,
  MatchPhase.PlayoffFinal,
  MatchPhase.FACupSemiFinal,
  MatchPhase.FACupFinal,
  MatchPhase.Other,
];
export const WORLD_CUP_PHASES: MatchPhase[] = [
  MatchPhase.WorldCupGroupStage,
  MatchPhase.WorldCupRoundOf32,
  MatchPhase.WorldCupRoundOf16,
  MatchPhase.WorldCupQuarterFinal,
  MatchPhase.WorldCupSemiFinal,
  MatchPhase.WorldCupThirdPlace,
  MatchPhase.WorldCupFinal,
];

export function competitionsForType(type: TournamentType | null | undefined): Competition[] {
  return type === TournamentType.FifaWorldCup ? WORLD_CUP_COMPETITIONS : ENGLAND_COMPETITIONS;
}

export function phasesForType(type: TournamentType | null | undefined): MatchPhase[] {
  return type === TournamentType.FifaWorldCup ? WORLD_CUP_PHASES : ENGLAND_PHASES;
}

/** World Cup phases (from the quarter-finals) that activate the Regra Flávio. */
export const WORLD_CUP_FLAVIO_PHASES: MatchPhase[] = [
  MatchPhase.WorldCupQuarterFinal,
  MatchPhase.WorldCupSemiFinal,
  MatchPhase.WorldCupThirdPlace,
  MatchPhase.WorldCupFinal,
];
