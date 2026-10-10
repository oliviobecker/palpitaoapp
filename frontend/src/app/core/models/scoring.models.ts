// The per-season, admin-editable scoring ruleset.
import { Competition, MatchPhase, ScoreCategory, TournamentType } from './enums';

export interface ScoringBasePoints {
  columnOnly: number;
  traditional: number;
  medium: number;
  uncommon: number;
  extraUncommon: number;
}

export interface ScoringScoreEntry {
  low: number;
  high: number;
  category: ScoreCategory;
}

export interface ScoringMultiplierRule {
  competition: Competition;
  phase: MatchPhase;
  multiplier: number;
  classicMultiplier: number;
}

export interface ScoringConfigTeam {
  teamId: string;
  name: string;
  shortName: string;
  isClassic: boolean;
  /**
   * The classic group the team belongs to, or null when it is not a classic. Only two teams
   * from the same group form a classic.
   */
  classicCompetition?: Competition | null;
}

/** A team designated as a classic, with the group it belongs to. */
export interface ScoringClassicTeamRequest {
  teamId: string;
  competition: Competition;
}

/**
 * The season's special rules: when the Flávio Rule starts applying and how absences
 * are punished. Defaults reproduce the classic Palpitão rules (16 / 1 / 20 / 5).
 */
export interface ScoringRules {
  /** First round the Flávio Rule applies to (England; the World Cup goes by phase). */
  flavioFromRound: number;
  /** First round in which an absence counts towards the punishment ladder. */
  absenceFromRound: number;
  /** Points deducted from the total per absence, from the 3rd one on. */
  absencePenaltyPoints: number;
  /** Absence ordinal that eliminates the participant from the season. */
  absenceEliminationCount: number;
}

export interface ScoringConfig {
  seasonId: string;
  seasonName: string;
  tournamentType: TournamentType;
  /** True when the season already has scored rounds — edits need a recalculate to take effect. */
  hasScoredRounds: boolean;
  basePoints: ScoringBasePoints;
  rules: ScoringRules;
  scoreEntries: ScoringScoreEntry[];
  multiplierRules: ScoringMultiplierRule[];
  /** Candidate classic teams for the season's tournament type, with selection. */
  teams: ScoringConfigTeam[];
}

export interface ScoringConfigRequest {
  basePoints: ScoringBasePoints;
  rules: ScoringRules;
  scoreEntries: ScoringScoreEntry[];
  multiplierRules: ScoringMultiplierRule[];
  classicTeams: ScoringClassicTeamRequest[];
}
