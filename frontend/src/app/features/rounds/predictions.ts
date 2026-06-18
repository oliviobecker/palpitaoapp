import { DatePipe } from '@angular/common';
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
import { RoundStatus, TournamentType, WORLD_CUP_FLAVIO_PHASES } from '../../core/models/enums';
import { Round, RoundMatch } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { PredictionsService } from '../../core/services/predictions.service';
import { RoundsService } from '../../core/services/rounds.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { Countdown } from '../../shared/components/countdown/countdown';
import { Loading } from '../../shared/components/loading/loading';
import { MultiplierBadge } from '../../shared/components/multiplier-badge/multiplier-badge';
import {
  computeMultiplier,
  isClassic,
  isLeagueOne,
  phaseLabel,
} from '../../shared/utils/match.util';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-predictions',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    TranslatePipe,
    CompetitionBadge,
    Countdown,
    Loading,
    MultiplierBadge,
  ],
  templateUrl: './predictions.html',
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
export class Predictions implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly predictionsApi = inject(PredictionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly multiplier = computeMultiplier;
  protected readonly classic = isClassic;
  protected readonly leagueOne = isLeagueOne;
  protected readonly phaseLabel = phaseLabel;

  protected readonly loading = signal(true);
  protected readonly round = signal<Round | null>(null);
  protected readonly matches = signal<RoundMatch[]>([]);

  /** Admin-only submission mode (from the round's season): participants can't submit. */
  protected readonly adminOnly = computed(
    () => this.round()?.allowParticipantsToSubmitPredictions === false,
  );
  /** World Cup certame (from the round's season): show the signed-prints notice. */
  protected readonly isWorldCup = computed(
    () => this.round()?.tournamentType === TournamentType.FifaWorldCup,
  );
  /** World Cup Flávio rule notice: the round has a match from the quarter-finals on. */
  protected readonly flavioActive = computed(
    () =>
      this.isWorldCup() && this.matches().some((m) => WORLD_CUP_FLAVIO_PHASES.includes(m.phase)),
  );
  protected readonly editable = signal(false);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  private isEdit = false;
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

  protected readonly form = this.fb.array<FormGroup>([]);

  protected readonly blockedMessage = computed(() => {
    const r = this.round();
    if (!r) return '';
    // Admin-only mode shows its own dedicated notice, not the round-status warnings.
    if (this.adminOnly()) return '';
    switch (r.status) {
      case RoundStatus.Draft:
        return 'predictions.blockedDraft';
      case RoundStatus.Locked:
        return 'predictions.blockedLocked';
      case RoundStatus.Scored:
        return 'predictions.blockedScored';
      case RoundStatus.Cancelled:
        return 'predictions.blockedCancelled';
      default:
        return this.editable() ? '' : 'predictions.blockedDeadline';
    }
  });

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    forkJoin({
      round: this.roundsApi.getById(this.roundId),
      mine: this.predictionsApi.getMine(this.roundId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ round, mine }) => {
          const sorted = [...round.matches].sort(
            (a, b) => a.order - b.order || a.startsAt.localeCompare(b.startsAt),
          );
          this.round.set(round);
          this.matches.set(sorted);

          const open = round.status === RoundStatus.Published;
          const beforeDeadline = round.firstMatchStartsAt
            ? new Date(round.firstMatchStartsAt).getTime() > Date.now()
            : false;
          // In admin-only mode the form is read-only — predictions come from the admin.
          this.editable.set(open && beforeDeadline && !this.adminOnly());
          this.isEdit = mine.predictions.length > 0;
          this.saved.set(this.isEdit);

          for (const match of sorted) {
            const existing = mine.predictions.find((p) => p.roundMatchId === match.id);
            this.form.push(
              this.fb.group({
                home: [
                  existing?.predictedHomeScore ?? null,
                  [Validators.required, Validators.min(0), Validators.pattern(/^\d+$/)],
                ],
                away: [
                  existing?.predictedAwayScore ?? null,
                  [Validators.required, Validators.min(0), Validators.pattern(/^\d+$/)],
                ],
              }),
            );
          }
          if (!this.editable()) {
            this.form.disable();
          }
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  group(i: number): FormGroup {
    return this.form.at(i) as FormGroup;
  }

  onInput(): void {
    this.saved.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error(this.translate.instant('predictions.fillAll'));
      return;
    }

    const items = this.matches().map((match, i) => ({
      roundMatchId: match.id,
      predictedHomeScore: Number(this.group(i).value.home),
      predictedAwayScore: Number(this.group(i).value.away),
    }));

    this.saving.set(true);
    const request$ = this.isEdit
      ? this.predictionsApi.update(this.roundId, items)
      : this.predictionsApi.save(this.roundId, items);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isEdit = true;
        this.saving.set(false);
        this.saved.set(true);
        this.toast.success(this.translate.instant('predictions.savedToast'));
      },
      error: () => this.saving.set(false),
    });
  }
}
