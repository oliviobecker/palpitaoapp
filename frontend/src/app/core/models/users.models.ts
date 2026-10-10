// Group participants, as the admin manages them.

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

export interface ParticipantRequest {
  name: string;
  email: string;
}
