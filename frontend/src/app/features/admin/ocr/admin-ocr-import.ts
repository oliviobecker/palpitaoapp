import { AdminUsersService } from '@core/services/admin/admin-users.service';
import { OcrImportsService } from '@core/services/admin/ocr-imports.service';
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
import { OcrBatch, OcrCandidate, Participant, Round } from '@core/models';
import { ConfirmService } from '@core/notifications/confirm.service';
import { ImageViewerService } from '@core/notifications/image-viewer.service';
import { ToastService } from '@core/notifications/toast.service';
import { OcrImageService } from '@core/services/ocr-image.service';
import { RoundsService } from '@core/services/rounds.service';
import { Icon } from '@shared/components/icon/icon';
import { Loading } from '@shared/components/loading/loading';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { isPendingOcrBatch } from '@shared/utils/ocr-batch.util';
import { AdminEntryNotice } from '../shared/admin-entry-notice';
import { adminEntryBlockKey } from '../shared/admin-entry.util';
import { AdminOcrBatches } from './admin-ocr-batches';
import { OcrUploadQueue } from './ocr-upload-queue';
import { commonParticipantId, validateOcrFile } from './admin-ocr-import.util';

/** Autosave lifecycle of one candidate card (debounced PUT per candidate). */
type SaveState = 'pending' | 'saving' | 'saved' | 'error';

const SAVE_DEBOUNCE_MS = 600;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-ocr-import',
  imports: [
    FormsModule,
    RouterLink,
    TranslatePipe,
    AdminEntryNotice,
    Icon,
    Loading,
    AdminOcrBatches,
    RoundLabelPipe,
  ],
  providers: [OcrUploadQueue],
  templateUrl: './admin-ocr-import.html',
  styleUrl: './admin-ocr-import.scss',
})
export class AdminOcrImport implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roundsApi = inject(RoundsService);
  private readonly usersApi = inject(AdminUsersService);
  private readonly ocrImportsApi = inject(OcrImportsService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmService);
  private readonly imageViewer = inject(ImageViewerService);
  private readonly ocrImages = inject(OcrImageService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly queue = inject(OcrUploadQueue);

  protected readonly loading = signal(true);
  protected readonly processing = signal(false);
  protected readonly confirming = signal(false);
  protected readonly round = signal<Round | null>(null);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly batch = signal<OcrBatch | null>(null);
  /** The screenshots picked and not yet sent — one, or a whole round's worth. */
  protected readonly files = signal<File[]>([]);
  /** The file when exactly one is picked: that case keeps the preview and opens its review. */
  protected readonly file = computed(() => (this.files().length === 1 ? this.files()[0] : null));
  /** Object URL of the file just picked, before it is uploaded. */
  protected readonly localPreviewUrl = signal<string | null>(null);
  /** Object URL of the image fetched back from the database (after a reload). */
  protected readonly storedPreviewUrl = signal<string | null>(null);
  protected readonly dragOver = signal(false);
  protected readonly saveStates = signal<Record<string, SaveState>>({});
  /** Participant applied to the whole batch (null = the cards disagree, or none is resolved). */
  protected readonly batchParticipant = signal<string | null>(null);
  protected language = 'por';
  protected roundId = '';

  private readonly saveTimers = new Map<string, ReturnType<typeof setTimeout>>();

  /** The local pick wins (no round trip); the stored copy covers a reload. */
  protected readonly previewUrl = computed(() => this.localPreviewUrl() ?? this.storedPreviewUrl());

  protected readonly needsReviewCount = computed(
    () => this.batch()?.candidates.filter((c) => c.needsReview).length ?? 0,
  );
  /**
   * Whose stored predictions a confirm would change, as the server last computed it (refreshed on
   * every saved edit, and confirm waits for pending edits).
   */
  protected readonly overwrites = computed(() => this.batch()?.overwrites ?? []);
  /** True while any candidate edit is unsaved (debounce pending, in flight or failed). */
  protected readonly hasUnsavedEdits = computed(() =>
    Object.values(this.saveStates()).some((s) => s !== 'saved'),
  );
  /**
   * Finalized, draft or cancelled: upload and confirm stay off (the notice says why). Reviewing
   * or discarding a batch left from before is still allowed — only confirming writes predictions.
   */
  protected readonly entryBlocked = computed(
    () => adminEntryBlockKey(this.round()?.status) !== null,
  );
  /** The name OCR read off the image, shown as a hint next to the batch selector. */
  protected readonly detectedName = computed(
    () => this.batch()?.candidates.find((c) => c.participantNameRaw)?.participantNameRaw ?? null,
  );

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    const batchId = this.route.snapshot.queryParamMap.get('batch');
    this.destroyRef.onDestroy(() => {
      this.revokeLocalPreview();
      this.ocrImages.releaseAll();
      this.saveTimers.forEach((t) => clearTimeout(t));
    });
    forkJoin({
      round: this.roundsApi.getById(this.roundId),
      participants: this.usersApi.listParticipants(),
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

    // A reload (or a link from the import history) lands here with only a batch id: restore
    // the review state from the server, image included.
    if (batchId) {
      this.ocrImportsApi
        .getOcrBatch(batchId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({ next: (b) => this.applyBatch(b) });
    }
  }

  /** Adopts a batch as the current review target and pulls its stored image if there is one. */
  private applyBatch(b: OcrBatch): void {
    this.batch.set(b);
    this.saveStates.set({});
    this.batchParticipant.set(commonParticipantId(b.candidates));
    if (b.hasImage && !this.localPreviewUrl()) {
      this.ocrImages
        .load(b.id)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (url) => this.storedPreviewUrl.set(url),
          // A missing/pruned image just leaves the preview card out — the review still works.
          error: () => this.storedPreviewUrl.set(null),
        });
    }
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.addFiles(Array.from(input.files ?? []));
    // Cleared so picking the same file again (after removing it) still fires a change.
    input.value = '';
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
    this.addFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  removeFile(f: File): void {
    this.setFiles(this.files().filter((x) => x !== f));
  }

  fileSize(f: File): string {
    return `${(f.size / 1024 / 1024).toFixed(1)} MB`;
  }

  openPreview(): void {
    const url = this.previewUrl();
    if (!url) {
      return;
    }
    const name = this.file()?.name ?? this.batch()?.originalFileName ?? 'image';
    this.imageViewer.open(url, {
      title: this.translate.instant('ocr.preview'),
      subtitle: name,
      downloadName: name,
    });
  }

  /** Adds the valid files to the pick, skipping one already there (same name and size). */
  private addFiles(picked: File[]): void {
    const current = this.files();
    const added = picked.filter(
      (f) => this.isValidFile(f) && !current.some((x) => x.name === f.name && x.size === f.size),
    );
    if (added.length > 0) {
      this.setFiles([...current, ...added]);
    }
  }

  private setFiles(files: File[]): void {
    this.revokeLocalPreview();
    this.files.set(files);
    // Previewed only when there is one: a round's worth of thumbnails is noise, and each file
    // name already says whose screenshot it is.
    this.localPreviewUrl.set(files.length === 1 ? URL.createObjectURL(files[0]) : null);
  }

  private isValidFile(f: File): boolean {
    const error = validateOcrFile(f.name, f.size);
    if (error) {
      this.toast.error(this.translate.instant(`ocr.${error}`));
      return false;
    }
    return true;
  }

  private revokeLocalPreview(): void {
    const url = this.localPreviewUrl();
    if (url) {
      URL.revokeObjectURL(url);
    }
  }

  process(): void {
    const files = this.files();
    if (files.length === 0) {
      this.toast.error(this.translate.instant('ocr.noFile'));
      return;
    }
    if (files.length > 1) {
      // A round's worth: each file becomes its own import through the queue, and lands in the
      // pending list below instead of opening a review per image.
      this.queue.enqueue(this.roundId, files, this.language);
      this.setFiles([]);
      return;
    }
    this.processing.set(true);
    this.ocrImportsApi
      .importImage(this.roundId, files[0], this.language)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (b) => {
          this.applyBatch(b);
          this.processing.set(false);
          this.announceIgnoredLines(b);
          // Keep the batch id in the URL so a reload comes back to this review, not the
          // upload form — the image is re-fetched from the database.
          this.showBatchInUrl(b.id);
        },
        error: () => this.processing.set(false),
      });
  }

  /** Opens one of the pending imports listed under the upload form. */
  openBatch(batchId: string): void {
    this.ocrImportsApi
      .getOcrBatch(batchId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (b) => {
          this.setFiles([]);
          this.applyBatch(b);
          this.showBatchInUrl(b.id);
        },
      });
  }

  /** Leaves the review for the upload form and the pending list, without touching the import. */
  backToList(): void {
    this.saveTimers.forEach((t) => clearTimeout(t));
    this.saveTimers.clear();
    this.batch.set(null);
    this.saveStates.set({});
    this.storedPreviewUrl.set(null);
    this.ocrImages.releaseAll();
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }

  private showBatchInUrl(batchId: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { batch: batchId },
      replaceUrl: true,
    });
  }

  /**
   * A screenshot holding two rounds (people send "Rodada 6 e 7" in one message) has the other
   * round's lines left out by the server — said here, so an empty-looking review is not a mystery.
   */
  private announceIgnoredLines(b: OcrBatch): void {
    const count = b.ignoredLineCount ?? 0;
    if (count > 0) {
      this.toast.info(
        this.translate.instant('ocr.ignoredOtherRound', {
          count,
          rounds: (b.ignoredRoundLabels ?? []).join(', '),
        }),
      );
    }
  }

  saveStateOf(id: string): SaveState | undefined {
    return this.saveStates()[id];
  }

  /**
   * Files every candidate against one participant. Each card still saves through the normal
   * debounced autosave, so the existing per-card state, retry and "unsaved edits" guard all
   * keep working — no batch endpoint needed.
   */
  applyParticipantToAll(userId: string | null): void {
    this.batchParticipant.set(userId);
    const b = this.batch();
    if (!b) {
      return;
    }
    // Two passes: every card is retargeted before the first save is scheduled, so the
    // re-derivation inside scheduleSave never sees a half-applied batch.
    const changed = b.candidates.filter((c) => (c.userId ?? null) !== userId);
    for (const c of changed) {
      c.userId = userId;
    }
    changed.forEach((c) => this.scheduleSave(c));
    // The cards bind to the same objects, but the mutation above is invisible to OnPush.
    this.batch.set({ ...b, candidates: [...b.candidates] });
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

  /**
   * Why a card needs a look: what is missing, and what the import doubted (a score its readings
   * disagreed on, a fixture matched approximately, a header naming someone other than the file).
   * A complete card can be flagged for the second reason alone.
   */
  reviewReasons(c: OcrCandidate): string {
    return [this.missingReasons(c), c.reviewNotes ?? ''].filter((p) => p.length > 0).join(' · ');
  }

  /** Debounced autosave: every edit lands on the server without a per-card save button. */
  scheduleSave(c: OcrCandidate, delay = SAVE_DEBOUNCE_MS): void {
    // A per-card participant change can agree with (or break) the batch-wide pick, so the
    // selector above is re-derived rather than left showing a stale name.
    const candidates = this.batch()?.candidates;
    if (candidates) {
      this.batchParticipant.set(commonParticipantId(candidates));
    }
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
    this.ocrImportsApi
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
            overwrites: server.overwrites ?? [],
            candidates: b.candidates.map((x) =>
              x.id === candidateId && serverCandidate
                ? {
                    ...x,
                    needsReview: serverCandidate.needsReview,
                    confidence: serverCandidate.confidence,
                    // Cleared by the server once the score or the fixture was touched.
                    reviewNotes: serverCandidate.reviewNotes,
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
    this.ocrImportsApi
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
                  overwrites: updated.overwrites ?? [],
                  candidates: cur.candidates.filter((x) => x.id !== c.id),
                }
              : cur,
          );
          this.toast.success(this.translate.instant('ocr.discarded'));
        },
      });
  }

  async confirm(): Promise<void> {
    const b = this.batch();
    if (!b || this.hasUnsavedEdits()) {
      return;
    }
    // A confirm overwrites stored predictions without keeping the old values: make the admin say
    // so, since a screenshot filed under the wrong person replaces that person's own round.
    const overwrites = this.overwrites();
    if (overwrites.length > 0) {
      const ok = await this.confirmDialog.ask(
        this.translate.instant('ocr.overwrite.confirmQuestion', {
          names: overwrites.map((o) => o.userName).join(', '),
        }),
        {
          title: this.translate.instant('ocr.overwrite.title'),
          confirmText: this.translate.instant('ocr.confirm'),
          danger: true,
        },
      );
      if (!ok) {
        return;
      }
    }
    this.confirming.set(true);
    this.ocrImportsApi
      .confirmOcr(b.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('ocr.confirmed'));
          this.confirming.set(false);
          this.afterConfirm(b.id);
        },
        error: () => this.confirming.set(false),
      });
  }

  /**
   * Back to the pending list while the round still has imports waiting — the admin is working
   * through a whole round's screenshots — and on to the round once it has none.
   */
  private afterConfirm(confirmedId: string): void {
    this.ocrImportsApi
      .listOcrBatches(this.roundId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (batches) => {
          const waiting = (Array.isArray(batches) ? batches : []).some(
            (x) => x.id !== confirmedId && isPendingOcrBatch(x.status),
          );
          if (waiting) {
            this.backToList();
          } else {
            void this.router.navigate(['/admin/rounds', this.roundId]);
          }
        },
        error: () => void this.router.navigate(['/admin/rounds', this.roundId]),
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
    this.ocrImportsApi
      .cancelOcr(b.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.batch.set(null);
          this.saveStates.set({});
          this.storedPreviewUrl.set(null);
          this.ocrImages.releaseAll();
          this.toast.success(this.translate.instant('ocr.cancelled'));
          void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: {},
            replaceUrl: true,
          });
        },
      });
  }
}
