// The global team catalogue and its sync with the external squad lists.
import { Competition, TeamType } from './enums';

export interface Team {
  id: string;
  name: string;
  shortName: string;
  isBigSevenClub: boolean;
  crestUrl?: string | null;
  /** League division the club plays in; null for clubs not tied to a tracked division. */
  division?: Competition | null;
  /** Club or national team; absent in older payloads, where a club is the safe reading. */
  teamType?: TeamType;
}

export interface TeamSyncCreate {
  name: string;
  shortName: string;
  isBigSevenClub: boolean;
}

export interface TeamSyncMove {
  teamId: string;
  name: string;
  fromDivision?: Competition | null;
  toDivision: Competition;
}

export interface TeamSyncNotFound {
  teamId: string;
  name: string;
}

export interface TeamSyncCompetition {
  competition: Competition;
  /** Distinct names the source returned; 0 means it had no data (off-season). */
  foundCount: number;
  toCreate: TeamSyncCreate[];
  toMove: TeamSyncMove[];
  /** Clubs the catalogue has here but the source did not list — reported, never removed. */
  notFound: TeamSyncNotFound[];
  unchangedCount: number;
}

export interface TeamSyncResponse {
  source: string;
  /** False for a preview, true once the changes were written. */
  applied: boolean;
  competitions: TeamSyncCompetition[];
}
