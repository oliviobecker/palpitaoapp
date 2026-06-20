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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { RoundStatus } from '../../core/models/enums';
import { Round, RoundMatch } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { GroupContextService } from '../../core/services/group-context.service';
import { RoundsService } from '../../core/services/rounds.service';
import { StandingsService } from '../../core/services/standings.service';
import { RefreshResultsResponse } from '../../core/models/models';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';
import { MatchList } from '../../shared/components/match-list/match-list';
import { RoundResultsEditor } from '../../shared/components/round-results-editor/round-results-editor';
import { RoundStatusBadge } from '../../shared/components/round-status-badge/round-status-badge';
import { buildRoundMessage } from '../../shared/utils/round-message.util';
import { buildClosingMessage } from '../../shared/utils/closing-message.util';
import { AdminRoundMessages } from './admin-round-messages';
import { RoundStepper } from './round-stepper';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-round-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    Icon,
    Loading,
    MatchList,
    RoundStatusBadge,
    AdminRoundMessages,
    RoundStepper,
    RoundResultsEditor,
  ],
  templateUrl: './admin-round-detail.html',
})
export class AdminRoundDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly standingsApi = inject(StandingsService);
  protected readonly group = inject(GroupContextService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly RoundStatus = RoundStatus;

  protected readonly loading = signal(true);
  protected readonly finalizing = signal(false);
  protected readonly refreshing = signal(false);
  protected readonly refreshSummary = signal<RefreshResultsResponse | null>(null);
  protected readonly round = signal<Round | null>(null);
  /** Sorted matches (stable reference per load) — feeds the inline results editor. */
  protected readonly matches = signal<RoundMatch[]>([]);
  protected readonly closing = signal('');
  private id = '';

  /** All matches have a result entered — gate for scoring (mirrors the backend rule). */
  protected readonly resultsComplete = computed(() => {
    const ms = this.matches();
    return ms.length > 0 && ms.every((m) => m.homeScore != null && m.awayScore != null);
  });
  /** How many matches still lack a result, for the "X of Y" hint. */
  protected readonly missingResults = computed(
    () => this.matches().filter((m) => m.homeScore == null || m.awayScore == null).length,
  );

  protected readonly form = this.fb.nonNullable.group({
    number: [1, [Validators.required, Validators.min(1)]],
    title: [''],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api
      .getById(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.round.set(r);
          this.matches.set(
            [...r.matches].sort(
              (a, b) => a.order - b.order || a.startsAt.localeCompare(b.startsAt),
            ),
          );
          this.form.setValue({ number: r.number, title: r.title ?? '' });
          this.loading.set(false);
          this.loadClosing(r);
        },
        error: () => this.loading.set(false),
      });
  }

  /** Once finalized, assemble the copy-ready closing message from results + standings. */
  private loadClosing(round: Round): void {
    if (round.status !== RoundStatus.Scored) {
      this.closing.set('');
      return;
    }
    forkJoin({
      results: this.api.getResults(round.id),
      standings: this.standingsApi.getStandings(round.seasonId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ results, standings }) =>
          this.closing.set(
            buildClosingMessage(round.number, results, standings, this.group.groupName() ?? ''),
          ),
        error: () => this.closing.set(''),
      });
  }

  saveMeta(): void {
    if (this.form.invalid) return;
    const { number, title } = this.form.getRawValue();
    this.api
      .update(this.id, { number, title: title || null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.after('roundDetail.updated'),
      });
  }

  publish(r: Round): void {
    this.api
      .publish(r.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.after('roundDetail.published') });
  }

  async lock(r: Round): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('roundDetail.confirmLock'), {
      title: this.translate.instant('roundDetail.lock'),
      confirmText: this.translate.instant('roundDetail.lock'),
    });
    if (!ok) {
      return;
    }
    this.api
      .lock(r.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.after('roundDetail.locked') });
  }

  async score(r: Round): Promise<void> {
    // Scoring runs only on a locked (or already-scored) round: locking is now an
    // explicit prior step, and results must be entered first (button is gated).
    const recalculating = r.status === RoundStatus.Scored;
    const ok = await this.confirm.ask(
      this.translate.instant(
        recalculating ? 'roundDetail.confirmRecalculate' : 'roundDetail.confirmFinalize',
      ),
      {
        title: this.translate.instant(
          recalculating ? 'roundDetail.recalculate' : 'roundDetail.finalize',
        ),
        confirmText: this.translate.instant(
          recalculating ? 'roundDetail.recalculate' : 'roundDetail.finalize',
        ),
      },
    );
    if (!ok) {
      return;
    }
    this.finalizing.set(true);
    this.api
      .score(r.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.finalizing.set(false);
          this.after(recalculating ? 'roundDetail.scored' : 'roundDetail.finalized');
        },
        error: () => this.finalizing.set(false),
      });
  }

  async cancel(r: Round): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('roundDetail.confirmCancel'), {
      title: this.translate.instant('roundDetail.cancelRound'),
      confirmText: this.translate.instant('roundDetail.cancelRound'),
      danger: true,
    });
    if (ok) {
      this.api
        .cancel(r.id)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({ next: () => this.after('roundDetail.cancelled') });
    }
  }

  async reopen(r: Round): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('roundDetail.confirmReopen'), {
      title: this.translate.instant('roundDetail.reopen'),
      confirmText: this.translate.instant('roundDetail.reopen'),
    });
    if (!ok) {
      return;
    }
    this.api
      .reopen(r.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.after('roundDetail.reopened') });
  }

  refreshResults(round: Round): void {
    this.refreshing.set(true);
    this.adminApi
      .refreshResults(round.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => {
          this.refreshSummary.set(summary);
          this.toast.success(summary.message);
          this.refreshing.set(false);
          this.load();
        },
        error: () => this.refreshing.set(false),
      });
  }

  message(round: Round): string {
    return buildRoundMessage(round, this.group.groupName() ?? '');
  }

  private after(key: string): void {
    this.toast.success(this.translate.instant(key));
    this.load();
  }
}
