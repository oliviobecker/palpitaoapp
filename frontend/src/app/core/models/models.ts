import {
  Competition,
  GroupRole,
  GroupUserStatus,
  MatchPhase,
  MatchStatus,
  RoundStatus,
  ScoreCategory,
  TournamentType,
  UserRole,
} from './enums';

export interface RegistrationRequest {
  /** GroupUser id of the membership request. */
  id: string;
  userId: string;
  name: string;
  email: string;
  createdAt: string;
  status: GroupUserStatus;
}

/** Public, non-sensitive view of an active group (registration picker). */
export interface PublicGroup {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
}

/** A group the authenticated user has approved access to. */
export interface MyGroup {
  groupId: string;
  groupName: string;
  slug: string;
  role: GroupRole;
  status: GroupUserStatus;
  /** Per-group active flag; false = deactivated by the group admin (blocked). */
  isActive: boolean;
}

export interface User {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: User;
}

export interface Season {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  /** The kind of certame this season runs (set on creation, immutable after). */
  tournamentType: TournamentType;
  /** Whether participants may view others' predictions (default false). */
  allowParticipantsToViewOthersPredictions: boolean;
  /** Whether participants submit predictions in the app (false = admin-only). */
  allowParticipantsToSubmitPredictions: boolean;
  /** True when participant-submitted predictions already exist (warn before disabling). */
  hasParticipantPredictions: boolean;
}

export interface Team {
  id: string;
  name: string;
  shortName: string;
  isBigSevenClub: boolean;
  crestUrl?: string | null;
  /** League division the club plays in; null for clubs not tied to a tracked division. */
  division?: Competition | null;
}

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

export interface RefreshResultsResponse {
  message: string;
  roundId: string;
  provider: string;
  providerEnabled: boolean;
  updatedMatches: number;
  finishedMatches: number;
  inProgressMatches: number;
  notStartedMatches: number;
  postponedMatches: number;
  cancelledMatches: number;
  temporaryStandingsUpdatedAt?: string | null;
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
}

export interface TemporaryStandings {
  roundId: string;
  isTemporary: boolean;
  roundStatus: RoundStatus;
  lastUpdatedAt?: string | null;
  computedMatches: number;
  remainingMatches: number;
  standings: TemporaryStanding[];
}

export interface RoundFlavio {
  applies: boolean;
  leaderNames: string[];
  deadlineUtc?: string | null;
}

export interface ScoutScoreGroup {
  homeScore: number;
  awayScore: number;
  names: string[];
}

export interface ScoutMatch {
  roundMatchId: string;
  homeTeamName: string;
  awayTeamName: string;
  groups: ScoutScoreGroup[];
}

export interface RoundScout {
  roundId: string;
  roundNumber: number;
  roundTitle?: string | null;
  matches: ScoutMatch[];
}

export interface Round {
  id: string;
  seasonId: string;
  number: number;
  title?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  status: RoundStatus;
  firstMatchStartsAt?: string | null;
  publishedAt?: string | null;
  lockedAt?: string | null;
  mirrorPublishedAt?: string | null;
  createdAt: string;
  matches: RoundMatch[];
  flavio?: RoundFlavio | null;
  /** From the round's season: the certame type (drives allowed competitions/phases). */
  tournamentType?: TournamentType;
  /** From the round's season: whether participants may view others' predictions. */
  allowParticipantsToViewOthersPredictions?: boolean;
  /** From the round's season: whether participants submit predictions in the app. */
  allowParticipantsToSubmitPredictions?: boolean;
}

export interface RoundSummary {
  id: string;
  seasonId: string;
  number: number;
  title?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  status: RoundStatus;
  firstMatchStartsAt?: string | null;
  publishedAt?: string | null;
  lockedAt?: string | null;
  matchCount: number;
  /** From the round's season: whether participants may view others' predictions. */
  allowParticipantsToViewOthersPredictions?: boolean;
  /** From the round's season: whether participants submit predictions in the app. */
  allowParticipantsToSubmitPredictions?: boolean;
}

export interface FixtureCandidate {
  externalId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamName: string;
  awayTeamName: string;
  startsAt: string;
  source: string;
  isBigSevenMatch: boolean;
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
  predictions: Prediction[];
}

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

export interface Absence {
  roundId: string;
  roundNumber: number;
  userId: string;
  absenceNumber: number;
  penaltyPoints: number;
  createdAt: string;
}

export interface Participant {
  id: string;
  name: string;
  email: string;
  isActive: boolean;
  isEliminated: boolean;
  totalPoints: number;
  absenceCount: number;
  penaltyPoints: number;
}

export interface OcrCandidate {
  id: string;
  userId?: string | null;
  participantNameRaw?: string | null;
  roundMatchId?: string | null;
  matchTextRaw?: string | null;
  predictedHomeScore?: number | null;
  predictedAwayScore?: number | null;
  confidence: number;
  needsReview: boolean;
  reviewNotes?: string | null;
}

export interface OcrBatch {
  id: string;
  roundId: string;
  status: string;
  languageUsed: string;
  originalFileName: string;
  extractedText?: string | null;
  createdAt: string;
  processedAt?: string | null;
  confirmedAt?: string | null;
  candidates: OcrCandidate[];
}

export interface AuditLog {
  id: string;
  userId?: string | null;
  userName?: string | null;
  action: string;
  entityName: string;
  entityId?: string | null;
  details?: string | null;
  createdAt: string;
}

export interface MatchScore {
  roundMatchId: string;
  basePoints: number;
  multiplier: number;
  finalPoints: number;
  scoreCategory: ScoreCategory;
  isExactScore: boolean;
  isCorrectColumn: boolean;
}

export interface RoundResultParticipant {
  userId: string;
  name: string;
  grossPoints: number;
  finalPoints: number;
  penaltyPoints: number;
  wasAbsent: boolean;
  wasEliminated: boolean;
  flavioRuleApplied: boolean;
  matchScores: MatchScore[];
}

export interface RoundResultMatch {
  roundMatchId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamName: string;
  awayTeamName: string;
  homeScore?: number | null;
  awayScore?: number | null;
  isFinished: boolean;
  multiplier: number;
}

export interface RoundResults {
  roundId: string;
  status: RoundStatus;
  matches: RoundResultMatch[];
  participants: RoundResultParticipant[];
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
