// A participant's own predictions and the mirror of everyone's.
import { Competition, MatchPhase, RoundStatus } from './enums';

export interface Prediction {
  roundMatchId: string;
  predictedHomeScore: number;
  predictedAwayScore: number;
  submittedAt: string;
  updatedAt?: string | null;
}

export interface MyPredictions {
  roundId: string;
  status: RoundStatus;
  firstMatchStartsAt?: string | null;
  /** General lock: one minute before the first kickoff (computed by the API). */
  predictionDeadlineUtc?: string | null;
  predictions: Prediction[];
}

export interface PredictionItem {
  roundMatchId: string;
  predictedHomeScore: number;
  predictedAwayScore: number;
}

export interface MirrorParticipant {
  userId: string;
  name: string;
  isAbsent: boolean;
  isEliminated: boolean;
  flavioRuleApplied: boolean;
  predictions: {
    roundMatchId: string;
    predictedHomeScore: number;
    predictedAwayScore: number;
    submittedAt: string;
  }[];
}

export interface Mirror {
  roundId: string;
  status: RoundStatus;
  /** 1..k for a part of a round played in parts: sending nothing there is not an absence alone. */
  part?: number;
  matches: {
    roundMatchId: string;
    competition: Competition;
    phase: MatchPhase;
    homeTeamName: string;
    awayTeamName: string;
    startsAt: string;
  }[];
  participants: MirrorParticipant[];
}
