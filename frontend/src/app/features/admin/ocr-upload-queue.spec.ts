import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OcrBatch } from '../../core/models/models';
import { AdminService } from '../../core/services/admin.service';
import { OcrUploadQueue } from './ocr-upload-queue';

const file = (name: string) => new File(['x'], name, { type: 'image/png' });

const batch = (id: string, userId: string, flagged = 0, ignored = 0): OcrBatch => ({
  id,
  roundId: 'r1',
  status: 'Processed',
  languageUsed: 'por',
  originalFileName: `${id}.png`,
  hasImage: true,
  createdAt: '2026-09-18T12:00:00Z',
  ignoredLineCount: ignored,
  candidates: [0, 1, 2].map((i) => ({
    id: `${id}-c${i}`,
    userId,
    confidence: 1,
    needsReview: i < flagged,
  })),
});

describe('OcrUploadQueue', () => {
  let importImage: ReturnType<typeof vi.fn>;
  let queue: OcrUploadQueue;

  beforeEach(() => {
    importImage = vi.fn();
    TestBed.configureTestingModule({
      providers: [OcrUploadQueue, { provide: AdminService, useValue: { importImage } }],
    });
    queue = TestBed.inject(OcrUploadQueue);
  });

  afterEach(() => vi.useRealTimers());

  it('sends one screenshot at a time and sums up each import', async () => {
    const first = new Subject<OcrBatch>();
    const responses: Observable<OcrBatch>[] = [first, of(batch('b2', 'p2', 1, 11))];
    importImage.mockImplementation(() => responses.shift());

    queue.enqueue('r1', [file('Valter.png'), file('Ezau.png')], 'por');
    await Promise.resolve();

    // The second waits for the first: OCR is CPU-bound, and the server throttles per admin.
    expect(importImage).toHaveBeenCalledTimes(1);
    expect(importImage).toHaveBeenCalledWith('r1', expect.any(File), 'por', { silent: true });

    first.next(batch('b1', 'p1'));
    first.complete();
    await vi.waitFor(() => expect(queue.busy()).toBe(false));

    expect(importImage).toHaveBeenCalledTimes(2);
    const [valter, ezau] = queue.items();
    expect(valter).toMatchObject({ status: 'done', rows: 3, needsReview: 0, participantId: 'p1' });
    expect(ezau).toMatchObject({ status: 'done', rows: 3, needsReview: 1, ignored: 11 });
    expect(queue.finished()).toBe(2);
  });

  it('waits out a throttled upload and tries the same screenshot again', async () => {
    vi.useFakeTimers();
    const throttled = new HttpErrorResponse({
      status: 429,
      headers: new HttpHeaders({ 'Retry-After': '2' }),
    });
    importImage
      .mockReturnValueOnce(throwError(() => throttled))
      .mockReturnValueOnce(of(batch('b1', 'p1')));

    queue.enqueue('r1', [file('Valter.png')], 'por');
    await vi.advanceTimersByTimeAsync(0);
    expect(queue.items()[0].status).toBe('waiting');

    await vi.advanceTimersByTimeAsync(2000);

    expect(importImage).toHaveBeenCalledTimes(2);
    expect(queue.items()[0].status).toBe('done');
  });

  it('marks a failed screenshot with the server message and carries on with the next', async () => {
    const rejected = new HttpErrorResponse({
      status: 422,
      error: { status: 422, message: 'Não foi possível processar a imagem.' },
    });
    importImage
      .mockReturnValueOnce(throwError(() => rejected))
      .mockReturnValueOnce(of(batch('b2', 'p2')));

    queue.enqueue('r1', [file('borrado.png'), file('Valter.png')], 'por');
    await vi.waitFor(() => expect(queue.busy()).toBe(false));

    expect(queue.items().map((i) => i.status)).toEqual(['error', 'done']);
    expect(queue.items()[0].error).toBe('Não foi possível processar a imagem.');

    queue.clearFinished();
    expect(queue.items()).toEqual([]);
  });
});
