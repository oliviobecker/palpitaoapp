import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { OcrBatchSummary } from '../../core/models/models';
import { ImageViewerService } from '../../core/notifications/image-viewer.service';
import { OcrImageService } from '../../core/services/ocr-image.service';
import { AdminService } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-ocr-history',
  imports: [DatePipe, RouterLink, TranslatePipe, Icon, EmptyState, ErrorState, SkeletonList],
  templateUrl: './admin-ocr-history.html',
  styles: [
    `
      .import-card {
        height: 100%;
      }
      .import-card__thumb {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 100%;
        height: 140px;
        border: 0;
        border-radius: 12px 12px 0 0;
        background: var(--surface-2);
        overflow: hidden;
        padding: 0;
      }
      .import-card__thumb--clickable {
        cursor: zoom-in;
      }
      .import-card__thumb img {
        width: 100%;
        height: 100%;
        object-fit: contain;
      }
    `,
  ],
})
export class AdminOcrHistory implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adminApi = inject(AdminService);
  private readonly ocrImages = inject(OcrImageService);
  private readonly imageViewer = inject(ImageViewerService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly batches = signal<OcrBatchSummary[]>([]);
  /** batchId -> object URL, populated only once an image has actually been opened. */
  protected readonly thumbs = signal<Record<string, string>>({});
  protected readonly opening = signal<string | null>(null);
  protected roundId = '';

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    this.destroyRef.onDestroy(() => this.ocrImages.releaseAll());
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.adminApi
      .listOcrBatches(this.roundId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (batches) => {
          this.batches.set(batches);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  /**
   * Fetches the bytes on demand. Without server-side thumbnails a gallery that eagerly loaded
   * every card would pull up to 10 MB each, so nothing is downloaded until it is asked for.
   */
  view(batch: OcrBatchSummary): void {
    if (!batch.hasImage) {
      return;
    }
    const cached = this.thumbs()[batch.id];
    if (cached) {
      this.openViewer(batch, cached);
      return;
    }
    this.opening.set(batch.id);
    this.ocrImages
      .load(batch.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (url) => {
          this.thumbs.update((t) => ({ ...t, [batch.id]: url }));
          this.opening.set(null);
          this.openViewer(batch, url);
        },
        error: () => {
          this.opening.set(null);
          // Mark it unavailable rather than toasting: a pruned image is expected, not an error.
          this.batches.update((list) =>
            list.map((b) => (b.id === batch.id ? { ...b, hasImage: false } : b)),
          );
        },
      });
  }

  private openViewer(batch: OcrBatchSummary, url: string): void {
    this.imageViewer.open(url, {
      title: batch.originalFileName,
      subtitle: this.uploadedBy(batch),
      downloadName: batch.originalFileName,
    });
  }

  protected uploadedBy(batch: OcrBatchSummary): string {
    const who = batch.uploadedByName ?? '—';
    return `${this.translate.instant('ocrHistory.uploadedBy')}: ${who}`;
  }

  sizeMb(bytes?: number | null): string {
    return bytes ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : '—';
  }

  /** Maps an OcrBatchStatus to a Bootstrap badge class. */
  statusClass(status: string): string {
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

  statusLabel(status: string): string {
    return this.translate.instant(`ocrHistory.status${status}`);
  }

  /** A batch that was never confirmed or cancelled can still be reopened for review. */
  isReviewable(batch: OcrBatchSummary): boolean {
    return (
      batch.status !== 'Confirmed' && batch.status !== 'Failed' && batch.status !== 'Cancelled'
    );
  }
}
