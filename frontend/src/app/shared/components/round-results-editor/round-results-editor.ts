import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { RoundMatch } from '../../../core/models/models';
import { ToastService } from '../../../core/notifications/toast.service';
import { MatchesService } from '../../../core/services/matches.service';
import { CompetitionBadge } from '../competition-badge/competition-badge';

/**
 * Reusable match-score entry form. Renders one home×away input pair per match and
 * persists them via {@link MatchesService.setResult}. Used both on the dedicated
 * results page and inline in the round-detail "Locked" step so the score-entry UI
 * (and its save logic) lives in a single place.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-round-results-editor',
  imports: [ReactiveFormsModule, TranslatePipe, CompetitionBadge],
  template: `
    <form>
      <div class="vstack gap-2">
        @for (m of matches(); track m.id; let i = $index) {
          <div class="card">
            <div class="card-body" [formGroup]="group(i)">
              <div class="d-flex align-items-center gap-2 mb-3">
                <app-competition-badge [competition]="m.competition" />
                @if (m.isFinished) {
                  <span class="badge text-bg-success">{{ 'adminResults.finished' | translate }}</span>
                }
              </div>
              <div class="d-flex align-items-center gap-2">
                <span class="team-badge" [style.background]="teamColor(m.homeTeamName)">{{
                  abbr(m.homeTeamName)
                }}</span>
                <span class="fw-semibold team-name">{{ m.homeTeamName }}</span>
                <input type="number" min="0" class="form-control score-box" formControlName="home" />
                <span class="text-muted">×</span>
                <input type="number" min="0" class="form-control score-box" formControlName="away" />
                <span class="fw-semibold team-name text-end">{{ m.awayTeamName }}</span>
                <span class="team-badge" [style.background]="teamColor(m.awayTeamName)">{{
                  abbr(m.awayTeamName)
                }}</span>
              </div>
            </div>
          </div>
        }
      </div>

      <div class="d-grid mt-3">
        <button
          type="button"
          class="btn btn-soft-primary btn-lg"
          (click)="save()"
          [disabled]="form.invalid || saving()"
        >
          @if (saving()) {
            <span class="spinner-border spinner-border-sm me-2"></span>
          }
          💾 {{ 'adminResults.saveResults' | translate }}
        </button>
      </div>
    </form>
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
export class RoundResultsEditor {
  private readonly fb = inject(FormBuilder);
  private readonly matchesApi = inject(MatchesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  /** Matches to enter results for (already sorted by the caller). */
  readonly matches = input.required<RoundMatch[]>();
  /** Emitted after results are saved successfully, so the caller can reload. */
  readonly saved = output<void>();

  protected readonly saving = signal(false);
  protected readonly form = this.fb.array<FormGroup>([]);

  constructor() {
    // Rebuild the score form whenever the matches input changes (e.g. after a reload).
    effect(() => {
      const matches = this.matches();
      this.form.clear();
      for (const m of matches) {
        this.form.push(
          this.fb.group({
            home: [m.homeScore ?? 0, [Validators.required, Validators.min(0)]],
            away: [m.awayScore ?? 0, [Validators.required, Validators.min(0)]],
          }),
        );
      }
    });
  }

  group(i: number): FormGroup {
    return this.form.at(i) as FormGroup;
  }

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

  save(): void {
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
          this.saved.emit();
        },
        error: () => this.saving.set(false),
      });
  }
}
