// Enums mirror the backend (serialized as strings via JsonStringEnumConverter).

export enum UserRole {
  Admin = 'Admin',
  Participant = 'Participant',
}

export enum UserStatus {
  PendingApproval = 'PendingApproval',
  Approved = 'Approved',
  Rejected = 'Rejected',
  Inactive = 'Inactive',
}

export enum GroupRole {
  GroupAdmin = 'GroupAdmin',
  Participant = 'Participant',
}

export enum GroupUserStatus {
  PendingApproval = 'PendingApproval',
  Approved = 'Approved',
  Rejected = 'Rejected',
  Inactive = 'Inactive',
}

/** The kind of certame a group runs. Mirrors the backend TournamentType. */
export enum TournamentType {
  PalpitaoEngland = 'PalpitaoEngland',
  FifaWorldCup = 'FifaWorldCup',
}

export enum TeamType {
  Club = 'Club',
  NationalTeam = 'NationalTeam',
}

export enum Competition {
  PremierLeague = 'PremierLeague',
  FACup = 'FACup',
  Championship = 'Championship',
  LeagueOne = 'LeagueOne',
  FifaWorldCup = 'FifaWorldCup',
}

export enum MatchPhase {
  Regular = 'Regular',
  PlayoffSemiFinal = 'PlayoffSemiFinal',
  PlayoffFinal = 'PlayoffFinal',
  FACupSemiFinal = 'FACupSemiFinal',
  FACupFinal = 'FACupFinal',
  Other = 'Other',
  WorldCupGroupStage = 'WorldCupGroupStage',
  WorldCupRoundOf32 = 'WorldCupRoundOf32',
  WorldCupRoundOf16 = 'WorldCupRoundOf16',
  WorldCupQuarterFinal = 'WorldCupQuarterFinal',
  WorldCupSemiFinal = 'WorldCupSemiFinal',
  WorldCupThirdPlace = 'WorldCupThirdPlace',
  WorldCupFinal = 'WorldCupFinal',
}

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

export enum RoundStatus {
  Draft = 'Draft',
  Published = 'Published',
  Locked = 'Locked',
  Scored = 'Scored',
  Cancelled = 'Cancelled',
}

export enum MatchStatus {
  NotStarted = 'NotStarted',
  InProgress = 'InProgress',
  Finished = 'Finished',
  Postponed = 'Postponed',
  Cancelled = 'Cancelled',
}

export enum ScoreCategory {
  None = 'None',
  ColumnOnly = 'ColumnOnly',
  Traditional = 'Traditional',
  Medium = 'Medium',
  Uncommon = 'Uncommon',
  ExtraUncommon = 'ExtraUncommon',
}
