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
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { Participant, Round, RoundMatch } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import {
  AdminParticipantPredictions,
  AdminService,
  ManualPredictionItem,
} from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';
import { AdminEntryNotice } from './admin-entry-notice';
import { adminEntryBlockKey } from './admin-entry.util';

/** One match's pair of score boxes, as the form holds them. */
export interface ScoreEntry {
  home: unknown;
  away: unknown;
}

/** The number typed in a score box, or null while it is empty — an empty box is never 0. */
export function typedScore(value: unknown): number | null {
  if (value === null || value === undefined || (typeof value === 'string' && !value.trim())) {
    return null;
  }
  const score = Number(value);
  return Number.isFinite(score) ? score : null;
}

/** How many matches still have an empty score box, on either side. */
export function missingScoreCount(entries: readonly ScoreEntry[]): number {
  return entries.filter((e) => typedScore(e.home) === null || typedScore(e.away) === null).length;
}

/**
 * The request rows for every match, or null while any score box is empty. An untyped box can
 * never reach the API as 0: that is how a line the OCR import missed was saved as a real 0x0.
 */
export function manualPredictionItems(
  matches: readonly Pick<RoundMatch, 'id'>[],
  entries: readonly ScoreEntry[],
): ManualPredictionItem[] | null {
  const items: ManualPredictionItem[] = [];
  for (const [i, match] of matches.entries()) {
    const home = typedScore(entries[i]?.home);
    const away = typedScore(entries[i]?.away);
    if (home === null || away === null) {
      return null;
    }
    items.push({ roundMatchId: match.id, predictedHomeScore: home, predictedAwayScore: away });
  }
  return items;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-manual-predictions',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    RouterLink,
    TranslatePipe,
    AdminEntryNotice,
    CompetitionBadge,
    Icon,
    Loading,
  ],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        <a [routerLink]="['/admin/rounds', roundId]"
          >{{ 'dashboard.round' | translate }} {{ round()?.number }}</a
        >
        · {{ 'manual.crumb' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-0">{{ 'manual.title' | translate }}</h1>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (round(); as r) {
      <app-admin-entry-notice [round]="r" />
      <div class="card mb-3">
        <div class="card-body p-4">
          <label for="mp-participant" class="form-label">{{
            'manual.participant' | translate
          }}</label>
          <div class="input-group input-group-lg">
            <span class="input-group-text"><app-icon name="user" [size]="16" /></span>
            <select
              id="mp-participant"
              class="form-select"
              [ngModel]="userId"
              (ngModelChange)="onParticipantChange($event)"
            >
              <option value="">{{ 'manual.selectParticipant' | translate }}</option>
              @for (p of participants(); track p.id) {
                <option [value]="p.id">
                  {{ p.name
                  }}{{ p.isEliminated ? ' (' + ('standings.eliminated' | translate) + ')' : '' }}
                </option>
              }
            </select>
          </div>
          @if (loadingExisting()) {
            <div class="small text-muted mt-2">{{ 'manual.loadingPredictions' | translate }}</div>
          } @else if (existing()?.hasPredictions) {
            <div class="alert alert-warning py-2 px-3 small mb-0 mt-2">
              {{ 'manual.prefilledNote' | translate }}
              @if (withoutPrediction() > 0) {
                <div class="fw-semibold mt-1">
                  {{ 'manual.partialNote' | translate: { count: withoutPrediction() } }}
                </div>
              }
            </div>
          }
        </div>
      </div>

      <form>
        <div class="vstack gap-2">
          @for (m of matches(); track m.id; let i = $index) {
            <div class="card">
              <div class="card-body" [formGroup]="group(i)">
                <div class="d-flex align-items-center gap-2 mb-3">
                  <app-competition-badge [competition]="m.competition" />
                  <small class="text-muted">{{ m.homeTeamName }} x {{ m.awayTeamName }}</small>
                </div>
                <div class="d-flex align-items-center gap-2">
                  <span class="team-badge" [style.background]="teamColor(m.homeTeamName)">{{
                    abbr(m.homeTeamName)
                  }}</span>
                  <span class="fw-semibold team-name">{{ m.homeTeamName }}</span>
                  <input
                    type="number"
                    min="0"
                    placeholder="–"
                    class="form-control score-box"
                    formControlName="home"
                    [class.is-invalid]="
                      group(i).controls['home'].touched && group(i).controls['home'].invalid
                    "
                  />
                  <span class="text-muted">×</span>
                  <input
                    type="number"
                    min="0"
                    placeholder="–"
                    class="form-control score-box"
                    formControlName="away"
                    [class.is-invalid]="
                      group(i).controls['away'].touched && group(i).controls['away'].invalid
                    "
                  />
                  <span class="fw-semibold team-name text-end">{{ m.awayTeamName }}</span>
                  <span class="team-badge" [style.background]="teamColor(m.awayTeamName)">{{
                    abbr(m.awayTeamName)
                  }}</span>
                </div>
              </div>
            </div>
          }
        </div>
      </form>

      <div class="card mt-3">
        <div class="card-body vstack gap-3">
          <div class="form-check">
            <input type="checkbox" class="form-check-input" id="ow" [(ngModel)]="overwrite" />
            <label class="form-check-label" for="ow">{{ 'manual.overwrite' | translate }}</label>
          </div>
          @if (selectedIsEliminated()) {
            <div>
              <input
                class="form-control"
                [placeholder]="'manual.justification' | translate"
                [(ngModel)]="justification"
              />
              <div class="form-text">{{ 'manual.eliminatedJustificationHint' | translate }}</div>
            </div>
          }
          <button
            class="btn btn-success btn-lg w-100"
            (click)="save()"
            [disabled]="saving() || !userId || entryBlocked()"
          >
            <app-icon name="save" [size]="16" /> {{ 'manual.save' | translate }}
          </button>
          @if (saveAttempted() && missingScores() > 0) {
            <div class="alert alert-danger py-2 px-3 small mb-0" role="alert">
              {{ 'manual.missingScores' | translate: { count: missingScores() } }}
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [
    `
      .team-name {
        flex: 1 1 0;
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .team-badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2rem;
        height: 2rem;
        border-radius: 8px;
        flex: none;
        color: #fff;
        font-size: 0.62rem;
        font-weight: 800;
        letter-spacing: 0.02em;
      }
      .score-box {
        width: 3.25rem;
        height: 3.25rem;
        flex: none;
        padding: 0;
        text-align: center;
        font-size: 1.4rem;
        font-weight: 700;
        border-radius: 12px;
      }
    `,
  ],
})
export class AdminManualPredictions implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roundsApi = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly round = signal<Round | null>(null);
  protected readonly matches = signal<RoundMatch[]>([]);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly existing = signal<AdminParticipantPredictions | null>(null);
  protected readonly loadingExisting = signal(false);
  protected readonly form = this.fb.array<FormGroup>([]);
  /** Finalized, draft or cancelled: the notice explains why, and saving would only be refused. */
  protected readonly entryBlocked = computed(
    () => adminEntryBlockKey(this.round()?.status) !== null,
  );
  /** Set when a save is refused for empty scores; the count under the button then stays live. */
  protected readonly saveAttempted = signal(false);
  /** Matches the loaded set has no prediction for — a line the OCR import missed, for one. */
  protected readonly withoutPrediction = computed(() => {
    const existing = this.existing();
    if (!existing?.hasPredictions) {
      return 0;
    }
    const predicted = new Set(existing.predictions.map((p) => p.roundMatchId));
    return this.matches().filter((m) => !predicted.has(m.id)).length;
  });

  protected userId = '';
  protected overwrite = false;
  protected justification = '';
  protected roundId = '';

  /** Three-letter team abbreviation for the badge, e.g. "Liverpool" → "LIV". */
  abbr(name: string): string {
    return (name.split(/\s+/)[0] ?? '').slice(0, 3).toUpperCase();
  }

  /** Deterministic colour per team name for the badge. */
  teamColor(name: string): string {
    let hash = 0;
    for (const ch of name) {
      hash = (hash * 31 + ch.charCodeAt(0)) % 360;
    }
    return `hsl(${hash}, 52%, 42%)`;
  }

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    forkJoin({
      round: this.roundsApi.getById(this.roundId),
      participants: this.adminApi.listParticipants(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ round, participants }) => {
          const sorted = [...round.matches].sort(
            (a, b) => a.order - b.order || a.startsAt.localeCompare(b.startsAt),
          );
          this.round.set(round);
          this.matches.set(sorted);
          this.participants.set(participants);
          for (const _ of sorted) {
            // Empty until typed or loaded: a box nobody filled in must never be saved as 0.
            this.form.push(
              this.fb.group({
                home: [null as number | null, [Validators.required, Validators.min(0)]],
                away: [null as number | null, [Validators.required, Validators.min(0)]],
              }),
            );
          }
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  group(i: number): FormGroup {
    return this.form.at(i) as FormGroup;
  }

  /** Matches with an empty score box; read by the template, so it follows every keystroke. */
  protected missingScores(): number {
    return missingScoreCount(this.form.getRawValue() as ScoreEntry[]);
  }

  /**
   * An eliminated participant is the one case that still needs the justified override — the
   * deadline no longer does (entry stays open until the round is finalized).
   */
  protected selectedIsEliminated(): boolean {
    return this.participants().find((p) => p.id === this.userId)?.isEliminated ?? false;
  }

  onParticipantChange(userId: string): void {
    this.userId = userId;
    this.existing.set(null);
    this.resetScores();
    this.saveAttempted.set(false);
    this.overwrite = false;
    this.justification = '';
    if (!userId) {
      return;
    }

    this.loadingExisting.set(true);
    this.adminApi
      .getParticipantPredictions(this.roundId, userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.existing.set(data);
          this.overwrite = data.hasPredictions;
          const matches = this.matches();
          for (const p of data.predictions) {
            const i = matches.findIndex((m) => m.id === p.roundMatchId);
            if (i >= 0) {
              this.group(i).patchValue({ home: p.predictedHomeScore, away: p.predictedAwayScore });
            }
          }
          if (data.hasPredictions) {
            // Only the matches the loaded set does not cover are still empty: flag them now.
            this.form.markAllAsTouched();
          }
          this.loadingExisting.set(false);
        },
        error: () => this.loadingExisting.set(false),
      });
  }

  /** Every box back to empty and untouched: a newly picked participant starts blank, not 0x0. */
  private resetScores(): void {
    this.form.reset();
  }

  save(): void {
    if (!this.userId) {
      return;
    }
    const predictions = manualPredictionItems(
      this.matches(),
      this.form.getRawValue() as ScoreEntry[],
    );
    if (!predictions || this.form.invalid) {
      // An empty score is refused, never sent as 0: flag the boxes and say how many are left.
      this.saveAttempted.set(true);
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);

    this.adminApi
      .saveManualPredictions(this.roundId, {
        userId: this.userId,
        predictions,
        overwriteExisting: this.overwrite,
        justification: this.justification || undefined,
        allowAfterDeadline: this.selectedIsEliminated() && !!this.justification,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success(this.translate.instant('manual.saved'));
          // Back to the round detail, where the coverage panel reflects the new entry.
          void this.router.navigate(['/admin/rounds', this.roundId]);
        },
        error: () => this.saving.set(false),
      });
  }
}
