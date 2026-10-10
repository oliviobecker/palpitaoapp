// The public standings link (no session; addressed by a season's public key).
import { RoundStatus, TournamentType } from './enums';
import type { MatchScore, RoundResultMatch } from './results.models';
import type { Standing } from './standings.models';

export interface PublicRoundSummary {
  number: number;
  /** 0 for a standalone round; 1..k for the parts of a round played in parts ("10.2"). */
  part?: number;
  title?: string | null;
  status: RoundStatus;
  startDate?: string | null;
  endDate?: string | null;
  /** False while the round is only locked: its points are still provisional. */
  isScored: boolean;
}

/** Base points per category, as configured for the season. */
export interface PublicRuleset {
  columnOnly: number;
  traditional: number;
  medium: number;
  uncommon: number;
  extraUncommon: number;
}

/** One scored round in a participant's history, as shown when a standings row opens. */
export interface PublicStandingRound {
  number: number;
  part?: number;
  points: number;
  wasAbsent: boolean;
  flavioRuleApplied: boolean;
}

/** Like {@link Standing}, plus the round-by-round history behind the accumulated total. */
export interface PublicStandingRow extends Standing {
  rounds: PublicStandingRound[];
}

export interface PublicSeason {
  groupName: string;
  seasonName: string;
  tournamentType: TournamentType;
  rounds: PublicRoundSummary[];
  ruleset: PublicRuleset;
}

/** Like {@link MatchScore}, plus the prediction the points came from. */
export interface PublicMatchScore extends MatchScore {
  predictedHomeScore?: number | null;
  predictedAwayScore?: number | null;
}

export interface PublicParticipantScore {
  userId: string;
  name: string;
  grossPoints: number;
  finalPoints: number;
  penaltyPoints: number;
  wasAbsent: boolean;
  wasEliminated: boolean;
  flavioRuleApplied: boolean;
  matchScores: PublicMatchScore[];
}

export interface PublicRound {
  number: number;
  part?: number;
  title?: string | null;
  status: RoundStatus;
  /** Locked but not scored: computed live, without absences/Flávio/elimination. */
  isPartial: boolean;
  computedMatches: number;
  remainingMatches: number;
  lastUpdatedAt?: string | null;
  matches: RoundResultMatch[];
  participants: PublicParticipantScore[];
}
