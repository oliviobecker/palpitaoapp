// Predictions grouped by scoreline, for the round scout.

export interface ScoutScoreGroup {
  homeScore: number;
  awayScore: number;
  names: string[];
}

export interface ScoutMatch {
  roundMatchId: string;
  homeTeamName: string;
  awayTeamName: string;
  startsAt: string;
  groups: ScoutScoreGroup[];
}

export interface RoundScout {
  roundId: string;
  roundNumber: number;
  roundPart?: number;
  roundTitle?: string | null;
  matches: ScoutMatch[];
}
