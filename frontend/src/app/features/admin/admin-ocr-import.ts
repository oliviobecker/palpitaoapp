import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { OcrBatch, OcrCandidate, Participant, Round } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  selector: 'app-admin-ocr-import',
  imports: [FormsModule, RouterLink, TranslatePipe, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        <a [routerLink]="['/admin/rounds', roundId]"
          >{{ 'dashboard.round' | translate }} {{ round()?.number }}</a
        >
        · {{ 'ocr.crumb' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-1">{{ 'ocr.title' | translate }}</h1>
      <p class="text-muted small mb-0">{{ 'ocr.subtitle' | translate }}</p>
    </div>

    @if (loading()) {
      <app-loading />
    } @else {
      <!-- Upload -->
      <div class="card mb-3">
        <div class="card-body p-4 vstack gap-3">
          <div>
            <label class="form-label">{{ 'ocr.language' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text">🔤</span>
              <select class="form-select" [(ngModel)]="language">
                <option value="por">{{ 'ocr.langPor' | translate }}</option>
                <option value="eng">{{ 'ocr.langEng' | translate }}</option>
                <option value="por+eng">{{ 'ocr.langBoth' | translate }}</option>
              </select>
            </div>
          </div>

          <div>
            <label class="form-label">{{ 'ocr.imageLabel' | translate }}</label>
            <label
              class="ocr-drop"
              [class.is-drag]="dragOver()"
              (dragover)="onDragOver($event)"
              (dragleave)="onDragLeave($event)"
              (drop)="onDrop($event)"
            >
              <span class="icon-tile icon-tile--blue">🖼️</span>
              <div class="ocr-drop__text">
                <span class="ocr-drop__cta">{{ 'ocr.dropClick' | translate }}</span>
                {{ 'ocr.dropRest' | translate }}
                <div class="ocr-drop__hint">{{ 'ocr.dropHint' | translate }}</div>
              </div>
              <input
                type="file"
                class="d-none"
                accept=".png,.jpg,.jpeg,.webp"
                (change)="onFile($event)"
              />
            </label>
          </div>

          @if (file(); as f) {
            <div class="file-chip">
              <span class="file-chip__icon">🖼️</span>
              <div class="flex-grow-1 min-w-0">
                <div class="fw-semibold text-truncate">{{ f.name }}</div>
                <small class="text-muted"
                  >{{ fileSize(f) }} · {{ 'ocr.fileReady' | translate }}</small
                >
              </div>
              <button type="button" class="file-chip__remove" (click)="removeFile()">×</button>
            </div>
          }

          <hr class="my-1" />

          <button
            class="btn btn-primary btn-lg w-100"
            (click)="process()"
            [disabled]="!file() || processing()"
          >
            @if (processing()) {
              <span class="spinner-border spinner-border-sm me-2"></span>
            }
            🧾 {{ 'ocr.process' | translate }}
          </button>

          <div class="tip-box">💡 {{ 'ocr.tip' | translate }}</div>
        </div>
      </div>

      @if (batch(); as b) {
        <!-- Extracted text -->
        <div class="card mb-3">
          <div class="card-body">
            <h2 class="h6 fw-bold mb-2">{{ 'ocr.extractedText' | translate }}</h2>
            <pre class="small text-body-secondary mb-0" style="white-space: pre-wrap">{{
              b.extractedText
            }}</pre>
          </div>
        </div>

        <!-- Candidates -->
        <h2 class="h6 fw-bold mb-2">
          {{ 'ocr.candidates' | translate }} ({{ b.candidates.length }})
        </h2>
        <div class="vstack gap-2">
          @for (c of b.candidates; track c.id) {
            <div class="card" [class.border-warning]="c.needsReview">
              <div class="card-body py-2 px-3 vstack gap-2">
                <div class="d-flex justify-content-between align-items-center">
                  <small class="text-muted"
                    >{{ c.matchTextRaw }} · {{ c.participantNameRaw }}</small
                  >
                  @if (c.needsReview) {
                    <span class="badge text-bg-warning">{{ 'ocr.needsReview' | translate }}</span>
                  }
                </div>

                <select class="form-select form-select-sm" [(ngModel)]="c.userId">
                  <option [ngValue]="null">{{ 'ocr.noParticipant' | translate }}</option>
                  @for (p of participants(); track p.id) {
                    <option [ngValue]="p.id">{{ p.name }}</option>
                  }
                </select>

                <select class="form-select form-select-sm" [(ngModel)]="c.roundMatchId">
                  <option [ngValue]="null">{{ 'ocr.noMatch' | translate }}</option>
                  @for (m of round()?.matches ?? []; track m.id) {
                    <option [ngValue]="m.id">{{ m.homeTeamName }} x {{ m.awayTeamName }}</option>
                  }
                </select>

                <div class="d-flex align-items-center gap-2">
                  <input
                    type="number"
                    min="0"
                    class="form-control form-control-sm text-center"
                    style="max-width:4rem"
                    [(ngModel)]="c.predictedHomeScore"
                  />
                  <span class="text-muted">x</span>
                  <input
                    type="number"
                    min="0"
                    class="form-control form-control-sm text-center"
                    style="max-width:4rem"
                    [(ngModel)]="c.predictedAwayScore"
                  />
                  <button
                    class="btn btn-sm btn-outline-secondary ms-auto"
                    (click)="saveCandidate(c)"
                  >
                    {{ 'common.save' | translate }}
                  </button>
                </div>
              </div>
            </div>
          }
        </div>

        <div class="d-grid gap-2 mt-3">
          <button class="btn btn-success btn-lg" (click)="confirm()">
            {{ 'ocr.confirm' | translate }}
          </button>
          <button class="btn btn-outline-danger" (click)="cancel()">
            {{ 'ocr.cancel' | translate }}
          </button>
        </div>
      }
    }
  `,
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
    }).subscribe({
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
    this.adminApi.importImage(this.roundId, f, this.language).subscribe({
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
      .subscribe({ next: (updated) => this.batch.set(updated) });
  }

  confirm(): void {
    const b = this.batch();
    if (!b) return;
    this.adminApi
      .confirmOcr(b.id)
      .subscribe({ next: () => this.toast.success(this.translate.instant('ocr.confirmed')) });
  }

  cancel(): void {
    const b = this.batch();
    if (!b) return;
    this.adminApi.cancelOcr(b.id).subscribe({
      next: () => {
        this.batch.set(null);
        this.toast.success(this.translate.instant('ocr.cancelled'));
      },
    });
  }
}
