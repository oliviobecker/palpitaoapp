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
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { RoundStatus } from '../../core/models/enums';
import { RoundSummary } from '../../core/models/models';
import { RoundsService } from '../../core/services/rounds.service';
import { GroupContextService } from '../../core/services/group-context.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Countdown } from '../../shared/components/countdown/countdown';
import { Icon } from '../../shared/components/icon/icon';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';
import {
  deadlinePassed,
  predictionDeadline,
  predictionDeadlineIso,
} from '../../shared/utils/deadline.util';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-rounds',
  imports: [
    RouterLink,
    DatePipe,
    TranslatePipe,
    EmptyState,
    ErrorState,
    Countdown,
    PageHeader,
    SkeletonList,
    Icon,
  ],
  templateUrl: './rounds.html',
})
export class Rounds implements OnInit {
  private readonly roundsApi = inject(RoundsService);
  protected readonly group = inject(GroupContextService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly RoundStatus = RoundStatus;

  statusKey(status: RoundStatus): string {
    return status.toLowerCase();
  }

  /** Prediction deadline (one minute before the first kickoff), for the card's countdown. */
  deadlineOf(round: RoundSummary): string | null {
    return predictionDeadlineIso(round);
  }

  /** Deadline within the final hour — used to flag the card's countdown as urgent. */
  isDeadlineUrgent(round: RoundSummary): boolean {
    const deadline = predictionDeadline(round);
    if (deadline === null) return false;
    const ms = deadline - Date.now();
    return ms > 0 && ms < 3_600_000;
  }

  /** Mirror is offered when the round's season allows it, or to group admins. */
  canViewOthers(round: RoundSummary): boolean {
    return round.allowParticipantsToViewOthersPredictions === true || this.group.isGroupAdmin();
  }

  /**
   * Whether an open round's mirror is already readable: the season flag releases it
   * live (from publication), and otherwise it opens on its own once predictions close
   * — the lock is manual, so the mirror must not wait for it.
   */
  canViewOpenMirror(round: RoundSummary): boolean {
    if (round.allowParticipantsToViewOthersPredictions === true) return true;
    return this.canViewOthers(round) && deadlinePassed(round);
  }

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly rounds = signal<RoundSummary[]>([]);

  // Participants only care about published/locked/scored rounds, newest first.
  protected readonly visible = computed(() =>
    this.rounds()
      .filter((r) => r.status !== RoundStatus.Draft && r.status !== RoundStatus.Cancelled)
      .sort((a, b) => b.number - a.number),
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.roundsApi
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.rounds.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }
}
