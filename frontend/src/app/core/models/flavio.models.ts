// The Flávio Rule and its per-round admin overrides.

export interface RoundFlavio {
  applies: boolean;
  leaderNames: string[];
  deadlineUtc?: string | null;
  /** The rule's window (24h, or 12h on short notice); null until the round is published. */
  windowHours?: number | null;
  /** True when the general lock cut the window short, so the window is not the real limit. */
  deadlineCappedByLock?: boolean;
}

export interface FlavioParticipant {
  userId: string;
  name: string;
  isTarget: boolean;
  submittedAt: string | null;
  grossPoints: number | null;
  finalPoints: number | null;
  flavioRuleApplied: boolean;
  isExempt: boolean;
  justification: string | null;
  updatedByUserId: string | null;
  updatedAt: string | null;
}

export interface RoundFlavioOverrides {
  roundId: string;
  applies: boolean;
  deadlineUtc: string | null;
  participants: FlavioParticipant[];
}
