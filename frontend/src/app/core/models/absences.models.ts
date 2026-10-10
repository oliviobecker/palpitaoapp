// Absences, their overrides and the absence review.
import { RoundStatus } from './enums';

export interface Absence {
  roundId: string;
  roundNumber: number;
  roundPart?: number;
  userId: string;
  absenceNumber: number;
  penaltyPoints: number;
  createdAt: string;
}

/** A closed round the participant can be recorded absent for when they are (re)activated. */
export interface AbsenceCandidateRound {
  roundId: string;
  number: number;
  part?: number;
  title?: string | null;
  status: RoundStatus;
  matchCount: number;
  predictionCount: number;
  /** Already scored: the absence only lands after a re-score or a season recalculation. */
  requiresRescore: boolean;
  /** An override currently marks the participant present for this round. */
  hasPresentOverride: boolean;
}

/** A closed round in which the participant counts as absent today, or carries an override. */
export interface AbsenceReviewRound {
  roundId: string;
  number: number;
  /** A part of a round played in parts only counts as an absence if every part is missed. */
  part?: number;
  title?: string | null;
  status: RoundStatus;
  matchCount: number;
  predictionCount: number;
  /** Effective state today: the override, else the automatic rule (sent nothing at all). */
  isAbsent: boolean;
  hasOverride: boolean;
  /** Already scored: changing it replays the whole season. */
  requiresRecalculation: boolean;
  /** Ordinal and penalty already on the ladder, when the round was scored with the absence. */
  absenceNumber?: number | null;
  penaltyPoints?: number | null;
}

/** The admin's explicit decision for one reviewed round. */
export interface AbsenceReviewDecision {
  roundId: string;
  isAbsent: boolean;
}

export interface AbsenceReviewResult {
  changedRounds: number;
  recalculated: boolean;
}
