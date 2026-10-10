// Predictions entered by an admin and the round's coverage.
import type { PredictionItem } from './predictions.models';

/** An admin-entered prediction has exactly the participant's shape. */
export type ManualPredictionItem = PredictionItem;

export interface ManualPredictionRequest {
  userId: string;
  predictions: ManualPredictionItem[];
  overwriteExisting: boolean;
  justification?: string;
  allowAfterDeadline?: boolean;
}

export interface AdminPredictionItem {
  roundMatchId: string;
  predictedHomeScore: number;
  predictedAwayScore: number;
  source: 'Participant' | 'AdminManual' | 'AdminOcr';
  updatedAt?: string;
}

export interface AdminParticipantPredictions {
  roundId: string;
  userId: string;
  hasPredictions: boolean;
  predictions: AdminPredictionItem[];
}

export interface PredictionCoverageParticipant {
  userId: string;
  name: string;
  predictedCount: number;
  /**
   * Scoring the round right now would zero them. Not derivable from `predictedCount`:
   * an admin override wins over the count, so the backend is the only source of truth.
   * On a round played in parts it is the whole round's verdict, decided by the last part.
   */
  willBeAbsent: boolean;
  /**
   * Absent in this round alone. Same as `willBeAbsent` on a standalone round; on a part it is
   * what the present/absent toggle flips, since that override belongs to the part.
   */
  absentInPart?: boolean;
  /** An admin decided this one by hand, so the flags above are not the automatic rule. */
  hasOverride: boolean;
}

/** Who has predicted the whole round vs. who is still missing (admin round detail). */
export interface PredictionCoverage {
  roundId: string;
  matchCount: number;
  totalParticipants: number;
  completeParticipants: number;
  missing: PredictionCoverageParticipant[];
}
