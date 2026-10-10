// Season standings and the live (temporary) standings of a round in play.
import { RoundStatus } from './enums';

export interface Standing {
  position: number;
  userId: string;
  name: string;
  totalPoints: number;
  playedRounds: number;
  absenceCount: number;
  penaltyPoints: number;
  isEliminated: boolean;
}

export interface TemporaryStanding {
  position: number;
  userId: string;
  name: string;
  roundTemporaryPoints: number;
  currentOfficialTotalPoints: number;
  projectedTotalPoints: number;
  computedMatches: number;
  remainingMatches: number;
  /** Heading for an absence when the round is scored. Projection: nothing was applied yet. */
  willBeAbsent: boolean;
}

export interface TemporaryStandings {
  roundId: string;
  roundNumber: number;
  /** 0 for a standalone round; 1..k for the parts of a round played in parts ("10.2"). */
  roundPart?: number;
  isTemporary: boolean;
  roundStatus: RoundStatus;
  lastUpdatedAt?: string | null;
  computedMatches: number;
  remainingMatches: number;
  standings: TemporaryStanding[];
}
