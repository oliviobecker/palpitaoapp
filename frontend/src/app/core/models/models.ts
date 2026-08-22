import {
  Competition,
  GroupRole,
  GroupUserStatus,
  MatchPhase,
  MatchStatus,
  RoundStatus,
  ScoreCategory,
  TeamType,
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
  /** Whether FA Cup fixtures are offered for this season (England certames only). */
  faCupEnabled: boolean;
  /** Credential of the public standings link (12 hex chars, unhyphenated). Admin-only. */
  publicKey: string;
  /** Whether the public link resolves. Off until an admin publishes it. */
  publicStandingsEnabled: boolean;
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
  /** Matches of the round the provider had nothing for — usually a club it spells differently. */
  unmatchedMatches: number;
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
  /** Heading for an absence when the round is scored. Projection: nothing was applied yet. */
  willBeAbsent: boolean;
}

export interface TemporaryStandings {
  roundId: string;
  roundNumber: number;
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
  /** The rule's window (24h, or 12h on short notice); null until the round is published. */
  windowHours?: number | null;
  /** True when the general lock cut the window short, so the window is not the real limit. */
  deadlineCappedByLock?: boolean;
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
  startsAt: string;
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
  /** General lock: one minute before the first kickoff (computed by the API). */
  predictionDeadlineUtc?: string | null;
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
  /** From the round's season: whether FA Cup fixtures may be added to this round. */
  faCupEnabled?: boolean;
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
  /** General lock: one minute before the first kickoff (computed by the API). */
  predictionDeadlineUtc?: string | null;
  publishedAt?: string | null;
  lockedAt?: string | null;
  matchCount: number;
  /** From the round's season: whether participants may view others' predictions. */
  allowParticipantsToViewOthersPredictions?: boolean;
  /** From the round's season: whether participants submit predictions in the app. */
  allowParticipantsToSubmitPredictions?: boolean;
  /** From the round's season: whether FA Cup fixtures may be added to this round. */
  faCupEnabled?: boolean;
}

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

/** A closed round the participant can be recorded absent for when they are (re)activated. */
export interface AbsenceCandidateRound {
  roundId: string;
  number: number;
  title?: string | null;
  status: RoundStatus;
  matchCount: number;
  predictionCount: number;
  /** Already scored: the absence only lands after a re-score or a season recalculation. */
  requiresRescore: boolean;
  /** An override currently marks the participant present for this round. */
  hasPresentOverride: boolean;
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
  /** False for pre-feature batches and for those whose image retention pruned. */
  hasImage: boolean;
  createdAt: string;
  processedAt?: string | null;
  confirmedAt?: string | null;
  candidates: OcrCandidate[];
}

/** One past import of a round, without the extracted text or the image bytes. */
export interface OcrBatchSummary {
  id: string;
  roundId: string;
  status: string;
  originalFileName: string;
  languageUsed: string;
  hasImage: boolean;
  imageContentType?: string | null;
  imageByteSize?: number | null;
  candidateCount: number;
  uploadedByUserId: string;
  uploadedByName?: string | null;
  createdAt: string;
  processedAt?: string | null;
  confirmedAt?: string | null;
}

/**
 * A name OCR import has been taught to read as a participant — learned when an admin confirms a
 * batch after correcting a name, or added by hand on the admin screen.
 */
export interface OcrParticipantAlias {
  id: string;
  /** Normalized lookup key (lowercased, accent-folded) — what a screenshot has to hit. */
  alias: string;
  /** The name as written/read, which is what the admin recognises. */
  aliasRaw: string;
  userId: string;
  userName: string;
  createdAt: string;
  updatedAt: string;
}

export interface PredictionCoverageParticipant {
  userId: string;
  name: string;
  predictedCount: number;
}

/** Who has predicted the whole round vs. who is still missing (admin round detail). */
export interface PredictionCoverage {
  roundId: string;
  matchCount: number;
  totalParticipants: number;
  completeParticipants: number;
  missing: PredictionCoverageParticipant[];
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
  /** Both teams are classic-eligible (drives the audit "classic" badge). */
  isClassic: boolean;
  /** An admin manual override set the multiplier. */
  isManualMultiplier: boolean;
}

export interface RoundResults {
  roundId: string;
  status: RoundStatus;
  matches: RoundResultMatch[];
  participants: RoundResultParticipant[];
}

// ---------------------------------------------------------------------------
// Public standings link (no session; addressed by a season's public key)
// ---------------------------------------------------------------------------

export interface PublicRoundSummary {
  number: number;
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

// --- Scoring configuration (per-season, admin-editable ruleset) ------------

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
