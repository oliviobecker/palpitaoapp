// Searching and importing fixtures from the external provider.
import { Competition, MatchPhase } from './enums';

export interface FixtureCandidate {
  externalId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamName: string;
  awayTeamName: string;
  startsAt: string;
  source: string;
  /** Both teams form a classic pair (Big Seven or the Championship rivals). */
  isClassicMatch: boolean;
  suggestedMultiplier: number;
  isAlreadyAddedToRound: boolean;
}

export interface SearchFixturesResponse {
  source: string;
  fixtures: FixtureCandidate[];
}

export interface ImportFixturesResponse {
  importedCount: number;
  skippedDuplicateCount: number;
  createdTeamCount: number;
  skippedDuplicates: string[];
}

export interface SearchFixturesRequest {
  startDate: string;
  endDate: string;
  competitions?: Competition[];
  /** Scopes the search to the round's season (and flags fixtures already added to it). */
  roundId?: string | null;
  /** Scopes the search while creating a round, where no round exists yet. */
  seasonId?: string | null;
}

export interface ImportFixtureItem {
  externalId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamName: string;
  awayTeamName: string;
  startsAt: string;
  source?: string;
}

export interface ImportFixturesRequest {
  fixtures: ImportFixtureItem[];
  leagueOneJustification?: string | null;
}
