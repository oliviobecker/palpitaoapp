import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { OcrBatchSummary, Participant } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';
import { isPendingOcrBatch, isReadyToConfirm } from '../../shared/utils/ocr-batch.util';
import { OcrUploadQueue } from './ocr-upload-queue';

/**
 * The multi-image side of the import page: the screenshots still uploading, and the round's
 * imports waiting for the admin. An import every row of which is settled can be confirmed from
 * here, one at a time or all together; anything flagged is opened for review first.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-ocr-batches',
  imports: [TranslatePipe, Icon, ErrorState, SkeletonList],
  templateUrl: './admin-ocr-batches.html',
  styles: [
    `
      .min-w-0 {
        min-width: 0;
      }
    `,
  ],
})
export class AdminOcrBatches {
  private readonly adminApi = inject(AdminService);
  private readonly confirmDialog = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly queue = inject(OcrUploadQueue);

  readonly roundId = input.required<string>();
  readonly participants = input<Participant[]>([]);
  /** The admin asked to open one of the pending imports. */
  readonly review = output<string>();

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly pending = signal<OcrBatchSummary[]>([]);
  /** An import (or "all") being confirmed or discarded right now. */
  protected readonly working = signal(false);
  protected readonly readyCount = computed(() => this.pending().filter(isReadyToConfirm).length);

  constructor() {
    // Loads on start, and again each time an upload of the queue finishes.
    effect(() => {
      this.queue.finished();
      untracked(() => this.load());
    });
  }

  load(): void {
    this.error.set(false);
    this.adminApi
      .listOcrBatches(this.roundId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (batches) => {
          this.pending.set(
            (Array.isArray(batches) ? batches : []).filter((b) => isPendingOcrBatch(b.status)),
          );
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  protected isReady(batch: OcrBatchSummary): boolean {
    return isReadyToConfirm(batch);
  }

  protected participantName(userId?: string | null): string | null {
    return this.participants().find((p) => p.id === userId)?.name ?? null;
  }

  protected async confirmOne(batch: OcrBatchSummary): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.adminApi.confirmOcr(batch.id));
      this.toast.success(this.translate.instant('ocr.confirmed'));
    });
  }

  protected async confirmReady(): Promise<void> {
    const ready = this.pending().filter(isReadyToConfirm);
    const ok = await this.confirmDialog.ask(
      this.translate.instant('ocr.pending.confirmReadyQuestion', { count: ready.length }),
      {
        title: this.translate.instant('ocr.pending.confirmReady', { count: ready.length }),
        confirmText: this.translate.instant('ocr.confirm'),
      },
    );
    if (!ok) {
      return;
    }
    await this.run(async () => {
      let confirmed = 0;
      for (const batch of ready) {
        try {
          await firstValueFrom(this.adminApi.confirmOcr(batch.id));
          confirmed++;
        } catch {
          // The interceptor already said why; the rest still go through.
        }
      }
      this.toast.success(this.translate.instant('ocr.pending.confirmedMany', { count: confirmed }));
    });
  }

  protected async discard(batch: OcrBatchSummary): Promise<void> {
    const ok = await this.confirmDialog.ask(this.translate.instant('ocr.confirmCancel'), {
      title: this.translate.instant('ocr.cancel'),
      confirmText: this.translate.instant('ocr.cancel'),
      danger: true,
    });
    if (!ok) {
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.adminApi.cancelOcr(batch.id));
      this.toast.success(this.translate.instant('ocr.cancelled'));
    });
  }

  /** One action at a time, and the list re-read afterwards whatever happened. */
  private async run(action: () => Promise<void>): Promise<void> {
    this.working.set(true);
    try {
      await action();
    } catch {
      // Reported by the error interceptor.
    } finally {
      this.working.set(false);
      this.load();
    }
  }
}
