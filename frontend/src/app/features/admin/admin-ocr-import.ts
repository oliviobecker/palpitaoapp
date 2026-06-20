import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { OcrBatch, OcrCandidate, Participant, Round } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-ocr-import',
  imports: [FormsModule, RouterLink, TranslatePipe, Icon, Loading],
  templateUrl: './admin-ocr-import.html',
  styles: [
    `
      .ocr-drop {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.6rem;
        text-align: center;
        width: 100%;
        padding: 1.5rem 1rem;
        border: 2px dashed var(--border-strong);
        border-radius: 14px;
        background: var(--surface-2);
        cursor: pointer;
        transition:
          border-color 0.15s ease,
          background 0.15s ease;
      }
      .ocr-drop:hover,
      .ocr-drop.is-drag {
        border-color: #2563eb;
        background: rgba(37, 99, 235, 0.05);
      }
      .ocr-drop__text {
        font-size: 0.82rem;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.03em;
        color: var(--ink-soft);
      }
      .ocr-drop__cta {
        color: #2563eb;
      }
      .ocr-drop__hint {
        margin-top: 0.4rem;
        font-size: 0.7rem;
        letter-spacing: 0.04em;
        color: var(--muted);
      }

      .file-chip {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.6rem 0.85rem;
        border: 1px solid var(--border);
        border-radius: 12px;
        background: var(--surface);
      }
      .file-chip__icon {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2.25rem;
        height: 2.25rem;
        border-radius: 10px;
        background: rgba(22, 163, 74, 0.14);
        flex: none;
      }
      .file-chip__remove {
        border: 0;
        background: transparent;
        color: var(--muted);
        font-size: 1.3rem;
        line-height: 1;
        cursor: pointer;
        flex: none;
      }
      .file-chip__remove:hover {
        color: var(--ink);
      }
      .min-w-0 {
        min-width: 0;
      }

      .tip-box {
        display: flex;
        gap: 0.5rem;
        padding: 0.75rem 0.9rem;
        border-radius: 12px;
        background: rgba(245, 158, 11, 0.1);
        border: 1px solid rgba(245, 158, 11, 0.22);
        color: #946100;
        font-size: 0.82rem;
      }
    `,
  ],
})
export class AdminOcrImport implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly processing = signal(false);
  protected readonly round = signal<Round | null>(null);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly batch = signal<OcrBatch | null>(null);
  protected readonly file = signal<File | null>(null);
  protected readonly previewUrl = signal<string | null>(null);
  protected readonly dragOver = signal(false);
  protected language = 'por';
  protected roundId = '';

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    forkJoin({
      round: this.roundsApi.getById(this.roundId),
      participants: this.adminApi.listParticipants(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ round, participants }) => {
          this.round.set(round);
          this.participants.set(participants);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.setFile(input.files?.[0] ?? null);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    this.setFile(event.dataTransfer?.files?.[0] ?? null);
  }

  removeFile(): void {
    this.setFile(null);
  }

  fileSize(f: File): string {
    return `${(f.size / 1024 / 1024).toFixed(1)} MB`;
  }

  private setFile(f: File | null): void {
    this.file.set(f);
    this.previewUrl.set(f ? URL.createObjectURL(f) : null);
  }

  process(): void {
    const f = this.file();
    if (!f) {
      this.toast.error(this.translate.instant('ocr.noFile'));
      return;
    }
    this.processing.set(true);
    this.adminApi
      .importImage(this.roundId, f, this.language)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (b) => {
          this.batch.set(b);
          this.processing.set(false);
        },
        error: () => this.processing.set(false),
      });
  }

  saveCandidate(c: OcrCandidate): void {
    const b = this.batch();
    if (!b) return;
    this.adminApi
      .updateOcrCandidate(b.id, c.id, {
        userId: c.userId ?? null,
        roundMatchId: c.roundMatchId ?? null,
        predictedHomeScore: c.predictedHomeScore ?? null,
        predictedAwayScore: c.predictedAwayScore ?? null,
        reviewNotes: c.reviewNotes ?? null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (updated) => this.batch.set(updated) });
  }

  confirm(): void {
    const b = this.batch();
    if (!b) return;
    this.adminApi
      .confirmOcr(b.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.toast.success(this.translate.instant('ocr.confirmed')) });
  }

  cancel(): void {
    const b = this.batch();
    if (!b) return;
    this.adminApi
      .cancelOcr(b.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.batch.set(null);
          this.toast.success(this.translate.instant('ocr.cancelled'));
        },
      });
  }
}
