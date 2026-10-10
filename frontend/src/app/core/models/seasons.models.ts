// Seasons (certames).
import { TournamentType } from './enums';

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

export interface SeasonRequest {
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  /** Set on creation and editable afterwards. */
  tournamentType: TournamentType;
  allowParticipantsToViewOthersPredictions: boolean;
  allowParticipantsToSubmitPredictions: boolean;
  /** Offer FA Cup fixtures for this season (England certames only). */
  faCupEnabled: boolean;
  /** Publish the public standings link (default false). The key itself is server-side only. */
  publicStandingsEnabled: boolean;
}
