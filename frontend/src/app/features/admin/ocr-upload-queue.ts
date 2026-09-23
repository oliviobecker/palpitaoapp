import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, OnDestroy, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { OcrBatch } from '../../core/models/models';
import { AdminService } from '../../core/services/admin.service';
import { ocrRetryDelayMs } from '../../shared/utils/ocr-batch.util';

export type OcrQueueStatus = 'queued' | 'processing' | 'waiting' | 'done' | 'error';

/** One screenshot of a multi-image upload, from picked to imported (or failed). */
export interface OcrQueueItem {
  key: number;
  file: File;
  language: string;
  status: OcrQueueStatus;
  /** Rows the import produced, once done. */
  rows?: number;
  /** Rows the import flagged for review, once done. */
  needsReview?: number;
  /** The participant every row is filed against (from the file name, usually), once done. */
  participantId?: string | null;
  /** Lines that were another round's fixtures and were left out, once done. */
  ignored?: number;
  error?: string;
}

/** Throttled uploads retried before the item is marked as failed. */
const MAX_ATTEMPTS = 5;

/**
 * Uploads a round's screenshots one at a time. One at a time on purpose: OCR is CPU-bound on the
 * server, which also throttles the endpoint per admin — a throttled upload waits out the window and
 * goes again instead of failing. Each screenshot becomes its own import, filed under the
 * participant its file name names, and lands in the pending list to be reviewed or confirmed.
 *
 * Provided by the import page, so the queue lives and dies with it.
 */
@Injectable()
export class OcrUploadQueue implements OnDestroy {
  private readonly adminApi = inject(AdminService);
  private nextKey = 0;
  private running = false;
  private destroyed = false;

  readonly items = signal<OcrQueueItem[]>([]);
  readonly busy = computed(() =>
    this.items().some(
      (i) => i.status === 'queued' || i.status === 'processing' || i.status === 'waiting',
    ),
  );
  /** Bumped each time an image finishes importing, so the pending list knows to reload. */
  readonly finished = signal(0);

  enqueue(roundId: string, files: readonly File[], language: string): void {
    const added = files.map<OcrQueueItem>((file) => ({
      key: this.nextKey++,
      file,
      language,
      status: 'queued',
    }));
    this.items.update((list) => [...list, ...added]);
    void this.run(roundId);
  }

  /** Clears the finished rows once the admin has read them. */
  clearFinished(): void {
    this.items.update((list) => list.filter((i) => i.status !== 'done' && i.status !== 'error'));
  }

  ngOnDestroy(): void {
    this.destroyed = true;
  }

  private async run(roundId: string): Promise<void> {
    if (this.running) {
      return;
    }
    this.running = true;
    try {
      for (let next = this.nextQueued(); next && !this.destroyed; next = this.nextQueued()) {
        await this.upload(roundId, next);
      }
    } finally {
      this.running = false;
    }
  }

  private nextQueued(): OcrQueueItem | undefined {
    return this.items().find((i) => i.status === 'queued');
  }

  private async upload(roundId: string, item: OcrQueueItem): Promise<void> {
    for (let attempt = 1; ; attempt++) {
      this.patch(item.key, { status: 'processing' });
      try {
        const batch = await firstValueFrom(
          this.adminApi.importImage(roundId, item.file, item.language, { silent: true }),
        );
        this.patch(item.key, { status: 'done', ...summarize(batch) });
        this.finished.update((n) => n + 1);
        return;
      } catch (error) {
        const wait = ocrRetryDelayMs(error);
        if (wait === null || attempt >= MAX_ATTEMPTS || this.destroyed) {
          this.patch(item.key, { status: 'error', error: messageOf(error) });
          return;
        }
        this.patch(item.key, { status: 'waiting' });
        await new Promise((resolve) => setTimeout(resolve, wait));
      }
    }
  }

  private patch(key: number, change: Partial<OcrQueueItem>): void {
    this.items.update((list) => list.map((i) => (i.key === key ? { ...i, ...change } : i)));
  }
}

function summarize(batch: OcrBatch): Partial<OcrQueueItem> {
  const users = new Set(batch.candidates.map((c) => c.userId ?? null));
  return {
    rows: batch.candidates.length,
    needsReview: batch.candidates.filter((c) => c.needsReview).length,
    participantId: users.size === 1 ? [...users][0] : null,
    ignored: batch.ignoredLineCount ?? 0,
  };
}

/** The server's own localized message when it sent one ({ status, message }). */
function messageOf(error: unknown): string | undefined {
  if (error instanceof HttpErrorResponse && typeof error.error?.message === 'string') {
    return error.error.message;
  }
  return undefined;
}
