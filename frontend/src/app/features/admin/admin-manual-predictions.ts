import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { Participant, Round, RoundMatch } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminParticipantPredictions, AdminService } from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-manual-predictions',
  imports: [ReactiveFormsModule, FormsModule, RouterLink, TranslatePipe, CompetitionBadge, Loading],
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
      <div class="card mb-3">
        <div class="card-body p-4">
          <label class="form-label">{{ 'manual.participant' | translate }}</label>
          <div class="input-group input-group-lg">
            <span class="input-group-text">👤</span>
            <select
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
                    class="form-control score-box"
                    formControlName="home"
                  />
                  <span class="text-muted">×</span>
                  <input
                    type="number"
                    min="0"
                    class="form-control score-box"
                    formControlName="away"
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
          @if (overwrite) {
            <input
              class="form-control"
              [placeholder]="'manual.justification' | translate"
              [(ngModel)]="justification"
            />
          }
          <button
            class="btn btn-success btn-lg w-100"
            (click)="save()"
            [disabled]="saving() || !userId"
          >
            💾 {{ 'manual.save' | translate }}
          </button>
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
            this.form.push(
              this.fb.group({
                home: [0, [Validators.required, Validators.min(0)]],
                away: [0, [Validators.required, Validators.min(0)]],
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

  onParticipantChange(userId: string): void {
    this.userId = userId;
    this.existing.set(null);
    this.resetScores();
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
          this.loadingExisting.set(false);
        },
        error: () => this.loadingExisting.set(false),
      });
  }

  private resetScores(): void {
    for (let i = 0; i < this.form.length; i++) {
      this.group(i).patchValue({ home: 0, away: 0 });
    }
  }

  save(): void {
    if (!this.userId || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const predictions = this.matches().map((m, i) => ({
      roundMatchId: m.id,
      predictedHomeScore: Number(this.group(i).value.home),
      predictedAwayScore: Number(this.group(i).value.away),
    }));

    this.adminApi
      .saveManualPredictions(this.roundId, {
        userId: this.userId,
        predictions,
        overwriteExisting: this.overwrite,
        justification: this.justification || undefined,
        allowAfterDeadline: this.overwrite && !!this.justification,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success(this.translate.instant('manual.saved'));
        },
        error: () => this.saving.set(false),
      });
  }
}
