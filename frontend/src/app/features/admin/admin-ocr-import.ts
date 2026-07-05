import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { OcrBatch, OcrCandidate, Participant, Round } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';

/** Autosave lifecycle of one candidate card (debounced PUT per candidate). */
type SaveState = 'pending' | 'saving' | 'saved' | 'error';

const MAX_FILE_MB = 10;
const ALLOWED_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp'];
const SAVE_DEBOUNCE_MS = 600;

/** Client-side mirror of the backend file rules, so a bad file fails before the upload. */
export function validateOcrFile(name: string, size: number): 'invalidFormat' | 'tooLarge' | null {
  const lower = name.toLowerCase();
  if (!ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext))) {
    return 'invalidFormat';
  }
  if (size > MAX_FILE_MB * 1024 * 1024) {
    return 'tooLarge';
  }
  return null;
}

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

      .ocr-preview-btn {
        display: block;
        width: 100%;
        padding: 0;
        border: 0;
        background: transparent;
      }
      .ocr-preview {
        width: 100%;
        max-height: 320px;
        object-fit: contain;
        border-radius: 12px;
        border: 1px solid var(--border);
        background: var(--surface-2);
        cursor: zoom-in;
      }
      .ocr-preview--expanded {
        max-height: none;
        cursor: zoom-out;
      }
      @media (min-width: 992px) {
        .ocr-sticky {
          position: sticky;
          top: 1rem;
        }
      }
    `,
  ],
})
export class AdminOcrImport implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roundsApi = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly processing = signal(false);
  protected readonly confirming = signal(false);
  protected readonly round = signal<Round | null>(null);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly batch = signal<OcrBatch | null>(null);
  protected readonly file = signal<File | null>(null);
  protected readonly previewUrl = signal<string | null>(null);
  protected readonly previewExpanded = signal(false);
  protected readonly dragOver = signal(false);
  protected readonly saveStates = signal<Record<string, SaveState>>({});
  protected language = 'por';
  protected roundId = '';

  private readonly saveTimers = new Map<string, ReturnType<typeof setTimeout>>();

  protected readonly needsReviewCount = computed(
    () => this.batch()?.candidates.filter((c) => c.needsReview).length ?? 0,
  );
  /** True while any candidate edit is unsaved (debounce pending, in flight or failed). */
  protected readonly hasUnsavedEdits = computed(() =>
    Object.values(this.saveStates()).some((s) => s !== 'saved'),
  );

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    this.destroyRef.onDestroy(() => {
      this.revokePreview();
      this.saveTimers.forEach((t) => clearTimeout(t));
    });
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

  togglePreview(): void {
    this.previewExpanded.set(!this.previewExpanded());
  }

  private setFile(f: File | null): void {
    if (f && !this.isValidFile(f)) {
      return;
    }
    this.revokePreview();
    this.file.set(f);
    this.previewUrl.set(f ? URL.createObjectURL(f) : null);
  }

  private isValidFile(f: File): boolean {
    const error = validateOcrFile(f.name, f.size);
    if (error) {
      this.toast.error(this.translate.instant(`ocr.${error}`));
      return false;
    }
    return true;
  }

  private revokePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
    }
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
          this.saveStates.set({});
          this.processing.set(false);
        },
        error: () => this.processing.set(false),
      });
  }

  saveStateOf(id: string): SaveState | undefined {
    return this.saveStates()[id];
  }

  confidencePct(c: OcrCandidate): number {
    return Math.round((c.confidence ?? 0) * 100);
  }

  missingReasons(c: OcrCandidate): string {
    const parts: string[] = [];
    if (!c.userId) {
      parts.push(this.translate.instant('ocr.missingParticipant'));
    }
    if (!c.roundMatchId) {
      parts.push(this.translate.instant('ocr.missingMatch'));
    }
    if (c.predictedHomeScore == null || c.predictedAwayScore == null) {
      parts.push(this.translate.instant('ocr.missingScore'));
    }
    return parts.join(' · ');
  }

  /** Debounced autosave: every edit lands on the server without a per-card save button. */
  scheduleSave(c: OcrCandidate, delay = SAVE_DEBOUNCE_MS): void {
    this.setSaveState(c.id, 'pending');
    const existing = this.saveTimers.get(c.id);
    if (existing) {
      clearTimeout(existing);
    }
    this.saveTimers.set(
      c.id,
      setTimeout(() => this.saveCandidate(c), delay),
    );
  }

  retrySave(c: OcrCandidate): void {
    this.scheduleSave(c, 0);
  }

  private saveCandidate(c: OcrCandidate): void {
    const b = this.batch();
    if (!b) {
      return;
    }
    this.saveTimers.delete(c.id);
    this.setSaveState(c.id, 'saving');
    this.adminApi
      .updateOcrCandidate(b.id, c.id, {
        userId: c.userId ?? null,
        roundMatchId: c.roundMatchId ?? null,
        predictedHomeScore: c.predictedHomeScore ?? null,
        predictedAwayScore: c.predictedAwayScore ?? null,
        reviewNotes: c.reviewNotes ?? null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.mergeCandidate(updated, c.id);
          this.setSaveState(c.id, 'saved');
        },
        error: () => this.setSaveState(c.id, 'error'),
      });
  }

  private setSaveState(id: string, state: SaveState): void {
    this.saveStates.update((s) => ({ ...s, [id]: state }));
  }

  /** Applies the server-recomputed flags for one candidate without clobbering
   *  in-progress local edits on the other cards. */
  private mergeCandidate(server: OcrBatch, candidateId: string): void {
    const serverCandidate = server.candidates.find((x) => x.id === candidateId);
    this.batch.update((b) =>
      b
        ? {
            ...b,
            status: server.status,
            candidates: b.candidates.map((x) =>
              x.id === candidateId && serverCandidate
                ? {
                    ...x,
                    needsReview: serverCandidate.needsReview,
                    confidence: serverCandidate.confidence,
                  }
                : x,
            ),
          }
        : b,
    );
  }

  async discard(c: OcrCandidate): Promise<void> {
    const b = this.batch();
    if (!b) {
      return;
    }
    const ok = await this.confirmDialog.ask(this.translate.instant('ocr.confirmDiscard'), {
      title: this.translate.instant('ocr.discard'),
      confirmText: this.translate.instant('ocr.discard'),
      danger: true,
    });
    if (!ok) {
      return;
    }
    const timer = this.saveTimers.get(c.id);
    if (timer) {
      clearTimeout(timer);
      this.saveTimers.delete(c.id);
    }
    this.adminApi
      .deleteOcrCandidate(b.id, c.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.saveStates.update((s) => {
            const { [c.id]: _removed, ...rest } = s;
            return rest;
          });
          this.batch.update((cur) =>
            cur
              ? {
                  ...cur,
                  status: updated.status,
                  candidates: cur.candidates.filter((x) => x.id !== c.id),
                }
              : cur,
          );
          this.toast.success(this.translate.instant('ocr.discarded'));
        },
      });
  }

  confirm(): void {
    const b = this.batch();
    if (!b || this.hasUnsavedEdits()) {
      return;
    }
    this.confirming.set(true);
    this.adminApi
      .confirmOcr(b.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('ocr.confirmed'));
          void this.router.navigate(['/admin/rounds', this.roundId]);
        },
        error: () => this.confirming.set(false),
      });
  }

  async cancel(): Promise<void> {
    const b = this.batch();
    if (!b) {
      return;
    }
    const ok = await this.confirmDialog.ask(this.translate.instant('ocr.confirmCancel'), {
      title: this.translate.instant('ocr.cancel'),
      confirmText: this.translate.instant('ocr.cancel'),
      danger: true,
    });
    if (!ok) {
      return;
    }
    this.adminApi
      .cancelOcr(b.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.batch.set(null);
          this.saveStates.set({});
          this.toast.success(this.translate.instant('ocr.cancelled'));
        },
      });
  }
}
