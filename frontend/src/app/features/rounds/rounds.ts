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
import { Loading } from '../../shared/components/loading/loading';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Countdown } from '../../shared/components/countdown/countdown';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-rounds',
  imports: [RouterLink, DatePipe, TranslatePipe, Loading, EmptyState, Countdown],
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

  /** Mirror is offered when the round's season allows it, or to group admins. */
  canViewOthers(round: RoundSummary): boolean {
    return round.allowParticipantsToViewOthersPredictions === true || this.group.isGroupAdmin();
  }

  /**
   * Live visibility: when the season flag is on, others' predictions are released
   * while the round is still open (before the lock), for everyone. Admins without
   * the flag still only see the mirror after the lock.
   */
  canViewLive(round: RoundSummary): boolean {
    return round.allowParticipantsToViewOthersPredictions === true;
  }

  protected readonly loading = signal(true);
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
    this.roundsApi
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.rounds.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
