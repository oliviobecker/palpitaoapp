// Rounds, their matches and rounds played in parts.
import { Competition, MatchPhase, MatchStatus, RoundStatus, TournamentType } from './enums';
import type { RoundFlavio } from './flavio.models';

export interface RoundMatch {
  id: string;
  roundId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  startsAt: string;
  order: number;
  homeScore?: number | null;
  awayScore?: number | null;
  isFinished: boolean;
  status?: MatchStatus;
  lastResultUpdatedAt?: string | null;
  manualMultiplierOverride?: number | null;
  manualMultiplierJustification?: string | null;
}

/** One round sharing a number with others: a part of a round played in parts. */
export interface RoundWeekPart {
  id: string;
  number: number;
  part: number;
  status: RoundStatus;
}

/** Preview of joining the previous round, leaving a round played in parts, or deleting the round. */
export interface RoundWeekMove {
  allowed: boolean;
  /** Where the round lands (none for a delete). */
  targetNumber?: number | null;
  targetPart?: number | null;
  /** Rounds whose number or part changes, the moved one included (a deleted one is not). */
  renumberedRounds: number;
  /**
   * A Scored round is affected (or a deleted round still holds results): the season is
   * recalculated in the same transaction.
   */
  requiresRecalculation: boolean;
}

/**
 * A round played in parts ("10.1" + "10.2") counts as one round for absences, decided by its
 * last part. What the round-detail screen needs to explain that and offer join/leave.
 */
export interface RoundWeek {
  /** Every round sharing the number (cancelled ones included), in part order. */
  parts: RoundWeekPart[];
  /** Standalone round or last part: finalizing it decides the absences. */
  decidesAbsences: boolean;
  /** Decides, but another part still takes predictions: it cannot be finalized yet. */
  openPartBlocksFinalize: boolean;
  /** A later part is already finalized: finalizing this one recalculates the season. */
  laterPartScored: boolean;
  joinPrevious: RoundWeekMove;
  leave: RoundWeekMove;
  /** Deleting the round: Draft or Cancelled only; the later rounds close the gap. */
  delete?: RoundWeekMove;
}

export interface Round {
  id: string;
  seasonId: string;
  number: number;
  /** 0 for a standalone round; 1..k for the parts of a round played in parts ("10.2"). */
  part?: number;
  title?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  status: RoundStatus;
  firstMatchStartsAt?: string | null;
  /** General lock: one minute before the first kickoff (computed by the API). */
  predictionDeadlineUtc?: string | null;
  publishedAt?: string | null;
  lockedAt?: string | null;
  mirrorPublishedAt?: string | null;
  createdAt: string;
  /** Cancelled rounds only: the status restoring it goes back to (the one it was cancelled from). */
  restoreStatus?: RoundStatus | null;
  matches: RoundMatch[];
  flavio?: RoundFlavio | null;
  /** From the round's season: the certame type (drives allowed competitions/phases). */
  tournamentType?: TournamentType;
  /** From the round's season: whether participants may view others' predictions. */
  allowParticipantsToViewOthersPredictions?: boolean;
  /** From the round's season: whether participants submit predictions in the app. */
  allowParticipantsToSubmitPredictions?: boolean;
  /** From the round's season: whether FA Cup fixtures may be added to this round. */
  faCupEnabled?: boolean;
  /** The round among the rounds sharing its number (absent from older API builds). */
  week?: RoundWeek;
}

export interface RoundSummary {
  id: string;
  seasonId: string;
  number: number;
  /** 0 for a standalone round; 1..k for the parts of a round played in parts ("10.2"). */
  part?: number;
  title?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  status: RoundStatus;
  firstMatchStartsAt?: string | null;
  /** General lock: one minute before the first kickoff (computed by the API). */
  predictionDeadlineUtc?: string | null;
  publishedAt?: string | null;
  lockedAt?: string | null;
  matchCount: number;
  /** From the round's season: whether participants may view others' predictions. */
  allowParticipantsToViewOthersPredictions?: boolean;
  /** From the round's season: whether participants submit predictions in the app. */
  allowParticipantsToSubmitPredictions?: boolean;
  /** From the round's season: whether FA Cup fixtures may be added to this round. */
  faCupEnabled?: boolean;
}
