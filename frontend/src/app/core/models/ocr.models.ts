// Prediction import from screenshots (OCR) and the learned participant aliases.

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
  /**
   * Lines that were another round's fixtures (a screenshot carrying two rounds) and were left
   * out. Only on the upload response — a reloaded batch does not carry it.
   */
  ignoredLineCount?: number;
  /** The rounds those lines belong to, as labels ("7", "10.1"). Only on the upload response. */
  ignoredRoundLabels?: string[];
  /**
   * Participants whose predictions confirming this batch would change. Only for a batch still
   * under review; empty when nothing stored would change.
   */
  overwrites?: OcrOverwrite[];
}

/**
 * A participant who already has predictions in the round, at least one of which a batch row would
 * replace with a different score — a confirm overwrites without asking and keeps no old values.
 */
export interface OcrOverwrite {
  userId: string;
  userName: string;
  /** Predictions the participant already has in the round. */
  existingCount: number;
  /** Rows of the batch that would replace one of them with a different score. */
  changedCount: number;
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
  /** Candidates still flagged for review; 0 means the batch can be confirmed as it is. */
  needsReviewCount?: number;
  /** The participant every candidate is filed against, or null when they differ or none is set. */
  participantUserId?: string | null;
  /** Rows that would replace an existing prediction with a different score (batches under review). */
  overwriteCount?: number;
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
