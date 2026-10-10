// Match results and a round's per-participant outcome.
import { Competition, MatchPhase, RoundStatus, ScoreCategory } from './enums';

export interface RefreshResultsResponse {
  message: string;
  roundId: string;
  provider: string;
  providerEnabled: boolean;
  updatedMatches: number;
  /** Matches of the round the provider had nothing for — usually a club it spells differently. */
  unmatchedMatches: number;
  finishedMatches: number;
  inProgressMatches: number;
  notStartedMatches: number;
  postponedMatches: number;
  cancelledMatches: number;
  temporaryStandingsUpdatedAt?: string | null;
}

export interface MatchScore {
  roundMatchId: string;
  basePoints: number;
  multiplier: number;
  finalPoints: number;
  scoreCategory: ScoreCategory;
  isExactScore: boolean;
  isCorrectColumn: boolean;
}

export interface RoundResultParticipant {
  userId: string;
  name: string;
  grossPoints: number;
  finalPoints: number;
  penaltyPoints: number;
  wasAbsent: boolean;
  wasEliminated: boolean;
  flavioRuleApplied: boolean;
  matchScores: MatchScore[];
}

export interface RoundResultMatch {
  roundMatchId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamName: string;
  awayTeamName: string;
  homeScore?: number | null;
  awayScore?: number | null;
  isFinished: boolean;
  multiplier: number;
  /** Both teams are classic-eligible (drives the audit "classic" badge). */
  isClassic: boolean;
  /** An admin manual override set the multiplier. */
  isManualMultiplier: boolean;
}

export interface RoundResults {
  roundId: string;
  status: RoundStatus;
  matches: RoundResultMatch[];
  participants: RoundResultParticipant[];
}
