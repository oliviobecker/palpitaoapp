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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { RoundStatus } from '../../core/models/enums';
import { Round, RoundMatch } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { MatchesService } from '../../core/services/matches.service';
import { RoundsService } from '../../core/services/rounds.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { Loading } from '../../shared/components/loading/loading';
import { RoundStatusBadge } from '../../shared/components/round-status-badge/round-status-badge';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-results',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    CompetitionBadge,
    Loading,
    RoundStatusBadge,
  ],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        <a [routerLink]="['/admin/rounds', id]"
          >{{ 'dashboard.round' | translate }} {{ round()?.number }}</a
        >
      </div>
      <div class="d-flex align-items-center gap-2">
        <h1 class="h4 fw-bold mb-0">{{ 'adminResults.title' | translate }}</h1>
        @if (round(); as r) {
          <app-round-status-badge [status]="r.status" />
        }
      </div>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (round(); as r) {
      @if (!canScore()) {
        <div class="alert alert-warning py-2">{{ 'adminResults.lockFirst' | translate }}</div>
      }

      <form>
        <div class="vstack gap-2">
          @for (m of matches(); track m.id; let i = $index) {
            <div class="card">
              <div class="card-body" [formGroup]="group(i)">
                <div class="d-flex align-items-center gap-2 mb-3">
                  <app-competition-badge [competition]="m.competition" />
                  @if (m.isFinished) {
                    <span class="badge text-bg-success">{{
                      'adminResults.finished' | translate
                    }}</span>
                  }
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

      @if (canScore()) {
        <div class="d-grid gap-2 mt-3">
          <button
            class="btn btn-soft-primary btn-lg"
            (click)="saveResults()"
            [disabled]="form.invalid || saving()"
          >
            💾 {{ 'adminResults.saveResults' | translate }}
          </button>
          <button class="btn btn-primary btn-lg" (click)="calculate()" [disabled]="scoring()">
            🧮 {{ 'adminResults.calculate' | translate }}
          </button>
        </div>
      }
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
export class AdminResults implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly matchesApi = inject(MatchesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly scoring = signal(false);
  protected readonly round = signal<Round | null>(null);
  protected readonly matches = signal<RoundMatch[]>([]);
  protected id = '';

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

  protected readonly form = this.fb.array<FormGroup>([]);
  protected readonly canScore = computed(() => {
    const s = this.round()?.status;
    return s === RoundStatus.Locked || s === RoundStatus.Scored;
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.roundsApi
      .getById(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          const sorted = [...r.matches].sort(
            (a, b) => a.order - b.order || a.startsAt.localeCompare(b.startsAt),
          );
          this.round.set(r);
          this.matches.set(sorted);
          this.form.clear();
          for (const m of sorted) {
            this.form.push(
              this.fb.group({
                home: [m.homeScore ?? 0, [Validators.required, Validators.min(0)]],
                away: [m.awayScore ?? 0, [Validators.required, Validators.min(0)]],
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

  saveResults(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const calls = this.matches().map((m, i) =>
      this.matchesApi.setResult(m.id, {
        homeScore: Number(this.group(i).value.home),
        awayScore: Number(this.group(i).value.away),
      }),
    );
    forkJoin(calls)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('adminResults.saved'));
          this.saving.set(false);
          this.load();
        },
        error: () => this.saving.set(false),
      });
  }

  calculate(): void {
    this.scoring.set(true);
    this.roundsApi
      .score(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('adminResults.calculated'));
          this.scoring.set(false);
          this.load();
        },
        error: () => this.scoring.set(false),
      });
  }
}
