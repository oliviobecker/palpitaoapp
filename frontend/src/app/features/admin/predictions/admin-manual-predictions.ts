import { AdminPredictionsService } from '@core/services/admin/admin-predictions.service';
import { AdminUsersService } from '@core/services/admin/admin-users.service';
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
import { AdminParticipantPredictions, Participant, Round, RoundMatch } from '@core/models';
import { ToastService } from '@core/notifications/toast.service';
import { RoundsService } from '@core/services/rounds.service';
import { CompetitionBadge } from '@shared/components/competition-badge/competition-badge';
import { Icon } from '@shared/components/icon/icon';
import { Loading } from '@shared/components/loading/loading';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { AdminEntryNotice } from '../shared/admin-entry-notice';
import { adminEntryBlockKey } from '../shared/admin-entry.util';
import {
  ScoreEntry,
  missingScoreCount,
  manualPredictionItems,
} from './admin-manual-predictions.util';
import { teamAbbr } from '@shared/utils/team-name.util';
import { avatarColor } from '@shared/utils/avatar.util';

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
    RoundLabelPipe,
  ],
  templateUrl: './admin-manual-predictions.html',
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
  private readonly adminPredictionsApi = inject(AdminPredictionsService);
  private readonly usersApi = inject(AdminUsersService);
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

  protected readonly abbr = teamAbbr;
  protected readonly teamColor = avatarColor;

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    forkJoin({
      round: this.roundsApi.getById(this.roundId),
      participants: this.usersApi.listParticipants(),
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
    this.adminPredictionsApi
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

    this.adminPredictionsApi
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
