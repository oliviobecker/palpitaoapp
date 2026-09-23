import { HttpErrorResponse } from '@angular/common/http';
import { OcrBatchSummary } from '../../core/models/models';

/** A batch that was never confirmed, cancelled or failed can still be reopened for review. */
export function isReviewableOcrBatch(status: string): boolean {
  return status !== 'Confirmed' && status !== 'Failed' && status !== 'Cancelled';
}

/**
 * A batch OCR has read and the admin has yet to settle: what the pending list shows, and the only
 * states the server lets an import be confirmed from.
 */
export function isPendingOcrBatch(status: string): boolean {
  return status === 'Processed' || status === 'Reviewed';
}

/**
 * A pending batch that can be confirmed straight from the list: it has rows, and none of them is
 * flagged — no missing participant, match or score, and no doubt the import raised about a score.
 */
export function isReadyToConfirm(batch: OcrBatchSummary): boolean {
  return (
    isPendingOcrBatch(batch.status) &&
    batch.candidateCount > 0 &&
    (batch.needsReviewCount ?? 0) === 0
  );
}

/** Maps an OcrBatchStatus to a Bootstrap badge class. */
export function ocrBatchStatusClass(status: string): string {
  switch (status) {
    case 'Confirmed':
      return 'text-bg-success';
    case 'Failed':
      return 'text-bg-danger';
    case 'Reviewed':
      return 'text-bg-info';
    case 'Processed':
      return 'text-bg-warning';
    default:
      return 'text-bg-secondary';
  }
}

/** Longest the queue waits out a rate limit before trying the same image again. */
export const OCR_RATE_LIMIT_WAIT_MS = 60_000;

/**
 * How long to wait before retrying an upload the server throttled (HTTP 429): what its
 * Retry-After header says, or the length of the throttle's window when it says nothing.
 * Null when the error is not a throttle at all.
 */
export function ocrRetryDelayMs(error: unknown): number | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 429) {
    return null;
  }
  const seconds = Number(error.headers?.get('Retry-After'));
  return Number.isFinite(seconds) && seconds > 0
    ? Math.min(seconds * 1000, OCR_RATE_LIMIT_WAIT_MS)
    : OCR_RATE_LIMIT_WAIT_MS;
}
