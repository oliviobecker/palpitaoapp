// Enums mirror the backend (serialized as strings via JsonStringEnumConverter).

export enum UserRole {
  Admin = 'Admin',
  Participant = 'Participant',
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
