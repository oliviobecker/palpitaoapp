import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { OcrBatchSummary } from '../../core/models/models';
import {
  OCR_RATE_LIMIT_WAIT_MS,
  isPendingOcrBatch,
  isReadyToConfirm,
  isReviewableOcrBatch,
  ocrRetryDelayMs,
} from './ocr-batch.util';

const summary = (over: Partial<OcrBatchSummary>): OcrBatchSummary => ({
  id: 'b1',
  roundId: 'r1',
  status: 'Processed',
  originalFileName: 'Valter.png',
  languageUsed: 'por',
  hasImage: true,
  candidateCount: 12,
  needsReviewCount: 0,
  uploadedByUserId: 'a1',
  createdAt: '2026-09-18T12:00:00Z',
  ...over,
});

describe('isReviewableOcrBatch', () => {
  it('reopens only what was neither confirmed, cancelled nor failed', () => {
    expect(isReviewableOcrBatch('Processed')).toBe(true);
    expect(isReviewableOcrBatch('Reviewed')).toBe(true);
    expect(isReviewableOcrBatch('Confirmed')).toBe(false);
    expect(isReviewableOcrBatch('Cancelled')).toBe(false);
    expect(isReviewableOcrBatch('Failed')).toBe(false);
  });
});

describe('isPendingOcrBatch', () => {
  it('lists what OCR read and nobody settled yet', () => {
    expect(isPendingOcrBatch('Processed')).toBe(true);
    expect(isPendingOcrBatch('Reviewed')).toBe(true);
    expect(isPendingOcrBatch('Uploaded')).toBe(false);
    expect(isPendingOcrBatch('Confirmed')).toBe(false);
  });
});

describe('isReadyToConfirm', () => {
  it('is ready when every row is settled', () => {
    expect(isReadyToConfirm(summary({}))).toBe(true);
  });

  it('waits for a flagged row, an empty batch or a closed one', () => {
    expect(isReadyToConfirm(summary({ needsReviewCount: 1 }))).toBe(false);
    expect(isReadyToConfirm(summary({ candidateCount: 0 }))).toBe(false);
    expect(isReadyToConfirm(summary({ status: 'Confirmed' }))).toBe(false);
  });

  it('never confirms from the list a batch that would replace stored predictions', () => {
    expect(isReadyToConfirm(summary({ overwriteCount: 3 }))).toBe(false);
    expect(isReadyToConfirm(summary({ overwriteCount: 0 }))).toBe(true);
  });
});

describe('ocrRetryDelayMs', () => {
  const throttled = (retryAfter?: string) =>
    new HttpErrorResponse({
      status: 429,
      headers: retryAfter ? new HttpHeaders({ 'Retry-After': retryAfter }) : new HttpHeaders(),
    });

  it('waits what the server asks for', () => {
    expect(ocrRetryDelayMs(throttled('12'))).toBe(12_000);
  });

  it('waits the whole window when the server does not say, and never longer', () => {
    expect(ocrRetryDelayMs(throttled())).toBe(OCR_RATE_LIMIT_WAIT_MS);
    expect(ocrRetryDelayMs(throttled('600'))).toBe(OCR_RATE_LIMIT_WAIT_MS);
  });

  it('does not retry an error that is not a throttle', () => {
    expect(ocrRetryDelayMs(new HttpErrorResponse({ status: 422 }))).toBeNull();
    expect(ocrRetryDelayMs(new Error('boom'))).toBeNull();
  });
});
