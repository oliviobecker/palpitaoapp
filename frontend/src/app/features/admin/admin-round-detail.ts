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
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { RoundStatus } from '../../core/models/enums';
import {
  PredictionCoverage,
  PredictionCoverageParticipant,
  Round,
  RoundMatch,
  ScoringConfig,
  Season,
} from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { GroupContextService } from '../../core/services/group-context.service';
import { RoundsService } from '../../core/services/rounds.service';
import { ScoringConfigService } from '../../core/services/scoring-config.service';
import { SeasonsService } from '../../core/services/seasons.service';
import { StandingsService } from '../../core/services/standings.service';
import { RefreshResultsResponse } from '../../core/models/models';
import { Icon } from '../../shared/components/icon/icon';
import { MatchList } from '../../shared/components/match-list/match-list';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { RoundResultsEditor } from '../../shared/components/round-results-editor/round-results-editor';
import { RoundStatusBadge } from '../../shared/components/round-status-badge/round-status-badge';
import { Skeleton } from '../../shared/components/skeleton/skeleton';
import { RoundLabelPipe } from '../../shared/pipes/round-label.pipe';
import { buildRoundMessage } from '../../shared/utils/round-message.util';
import { buildClosingMessage } from '../../shared/utils/closing-message.util';
import { publicStandingsUrl } from '../../shared/utils/public-link.util';
import { roundLabel } from '../../shared/utils/round-name.util';
import { AdminRoundMessages } from './admin-round-messages';
import { RoundStepper } from './round-stepper';
import { AdminFlavioOverrides } from './admin-flavio-overrides';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-round-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    Icon,
    MatchList,
    PageHeader,
    RoundStatusBadge,
    Skeleton,
    AdminRoundMessages,
    RoundStepper,
    RoundResultsEditor,
    AdminFlavioOverrides,
    RoundLabelPipe,
  ],
  templateUrl: './admin-round-detail.html',
  styles: [
    `
      .tools-card > summary {
        cursor: pointer;
        list-style: none;
        color: var(--ink);
      }
      .tools-card > summary::-webkit-details-marker {
        display: none;
      }
      .tools-card > summary::after {
        content: '';
        width: 0.5rem;
        height: 0.5rem;
        margin-left: auto;
        border-right: 2px solid var(--muted);
        border-bottom: 2px solid var(--muted);
        transform: rotate(-45deg);
        transition: transform 0.15s ease;
      }
      .tools-card[open] > summary::after {
        transform: rotate(45deg);
      }
    `,
  ],
})
export class AdminRoundDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly standingsApi = inject(StandingsService);
  private readonly seasonsApi = inject(SeasonsService);
  private readonly scoringApi = inject(ScoringConfigService);
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
  /** A join/leave is on its way: it may renumber rounds and replay the season. */
  protected readonly regrouping = signal(false);
  protected readonly refreshSummary = signal<RefreshResultsResponse | null>(null);
  protected readonly round = signal<Round | null>(null);
  /** Sorted matches (stable reference per load) — feeds the inline results editor. */
  protected readonly matches = signal<RoundMatch[]>([]);
  protected readonly closing = signal('');
  /** Who has predicted everything vs. who is missing — shown while Published and Locked. */
  protected readonly coverage = signal<PredictionCoverage | null>(null);
  /**
   * The season's ruleset, so the multipliers shown here (and in the group message) match a
   * customised season. Best-effort: without it the historical defaults apply.
   */
  protected readonly scoringConfig = signal<ScoringConfig | null>(null);
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
  /**
   * The last part of a round played in parts decides its absences from what every part received,
   * so it waits while another part still takes predictions (mirrors the backend guard).
   */
  protected readonly finalizeBlockedByPart = computed(() => {
    const r = this.round();
    return r?.status === RoundStatus.Locked && !!r.week?.openPartBlocksFinalize;
  });

  protected readonly form = this.fb.nonNullable.group({
    number: [1, [Validators.required, Validators.min(1)]],
    title: [''],
  });

  ngOnInit(): void {
    // Parts link to each other on this same route, so follow the id instead of reading it once.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.id = params.get('id') ?? '';
      this.refreshSummary.set(null);
      this.load();
    });
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
          // A part's number follows its round played in parts: it moves by ungrouping.
          if (r.part) {
            this.form.controls.number.disable();
          } else {
            this.form.controls.number.enable();
          }
          this.loading.set(false);
          this.loadClosing(r);
          this.loadCoverage(r);
          this.loadScoringConfig(r);
        },
        error: () => this.loading.set(false),
      });
  }

  /**
   * Prediction coverage helps decide when to chase stragglers before locking — and, while
   * Locked, it is the last chance to fix who scoring is about to mark absent, since an
   * absence there costs the round and a rung on the punishment ladder. Not loaded once
   * Scored: correcting history would mean a recalculation, which is a separate decision.
   */
  private loadCoverage(round: Round): void {
    if (round.status !== RoundStatus.Published && round.status !== RoundStatus.Locked) {
      this.coverage.set(null);
      return;
    }
    this.adminApi
      .getPredictionCoverage(round.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (cov) => this.coverage.set(cov),
        error: () => this.coverage.set(null),
      });
  }

  /** Best-effort: a failure just leaves the defaults in charge of the multipliers. */
  private loadScoringConfig(round: Round): void {
    this.scoringApi
      .get(round.seasonId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (config) => this.scoringConfig.set(config),
        error: () => this.scoringConfig.set(null),
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
      // The public link belongs in the message, not buried in the season settings: this is
      // the one moment the whole group is looking. list() avoids a new endpoint, and a
      // failure here must not cost the admin the message itself.
      seasons: this.seasonsApi.list().pipe(catchError(() => of([] as Season[]))),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ results, standings, seasons }) => {
          const season = seasons.find((s) => s.id === round.seasonId);
          const label = roundLabel(round.number, round.part);
          const link =
            season?.publicStandingsEnabled && season.publicKey
              ? publicStandingsUrl(season.publicKey, label)
              : '';
          this.closing.set(
            buildClosingMessage(label, results, standings, this.group.groupName() ?? '', link),
          );
        },
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
    let message = this.translate.instant(
      recalculating ? 'roundDetail.confirmRecalculate' : 'roundDetail.confirmFinalize',
    );
    // A later part already decided this round's absences: finalizing this one replays them.
    if (!recalculating && r.week?.laterPartScored) {
      message += ' ' + this.translate.instant('roundDetail.finalizeReplays');
    }
    const ok = await this.confirm.ask(message, {
      title: this.translate.instant(
        recalculating ? 'roundDetail.recalculate' : 'roundDetail.finalize',
      ),
      confirmText: this.translate.instant(
        recalculating ? 'roundDetail.recalculate' : 'roundDetail.finalize',
      ),
    });
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
    let message = this.translate.instant('roundDetail.confirmCancel');
    // Another part already scored: the part deciding the absences may change, so the server
    // replays the season.
    if (r.week?.parts.some((p) => p.id !== r.id && p.status === RoundStatus.Scored)) {
      message += ' ' + this.translate.instant('roundDetail.cancelPartRecalc');
    }
    const ok = await this.confirm.ask(message, {
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

  /** Undo of an early/accidental lock: steps the round back to Published. */
  async unlock(r: Round): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('roundDetail.confirmUnlock'), {
      title: this.translate.instant('roundDetail.unlock'),
      confirmText: this.translate.instant('roundDetail.unlock'),
    });
    if (!ok) {
      return;
    }
    this.api
      .unlock(r.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.after('roundDetail.unlocked') });
  }

  /**
   * The absence override, which the API has always exposed and no screen ever called — so
   * until now a wrong call could not be corrected from the app at all. There are only two
   * states, so a single button toggles; the confirmation spells out the direction and the
   * justification is mandatory (the backend requires and audits it).
   *
   * Caveat worth knowing: the backend upserts and offers no delete, so an override can be
   * flipped but never removed. From the first click on, that participant is on a manual
   * decision for this round rather than back on the automatic rule.
   */
  async toggleAbsence(round: Round, p: PredictionCoverageParticipant): Promise<void> {
    const markAbsent = !this.absentHere(p);
    const action = markAbsent ? 'roundDetail.markAbsent' : 'roundDetail.markPresent';
    const justification = await this.confirm.askWithInput(
      this.translate.instant(
        markAbsent ? 'roundDetail.markAbsentConfirm' : 'roundDetail.markPresentConfirm',
        { name: p.name },
      ),
      {
        title: this.translate.instant(action),
        confirmText: this.translate.instant(action),
        inputLabel: this.translate.instant('roundDetail.absenceJustification'),
        required: true,
      },
    );
    if (!justification) {
      return;
    }
    this.adminApi
      .overrideAbsence(round.id, { userId: p.userId, isAbsent: markAbsent, justification })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.after('roundDetail.absenceOverrideSaved') });
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
    return buildRoundMessage(round, this.group.groupName() ?? '', this.scoringConfig());
  }

  /**
   * Absent in this round alone — what the present/absent override flips, since the override
   * belongs to the round. On a part it differs from `willBeAbsent`, the whole round's verdict.
   */
  protected absentHere(p: PredictionCoverageParticipant): boolean {
    return p.absentInPart ?? p.willBeAbsent;
  }

  /** The part that decides the round's absences: the last one not cancelled. */
  protected lastPartLabel(r: Round): string {
    const live = (r.week?.parts ?? []).filter((p) => p.status !== RoundStatus.Cancelled);
    const last = live[live.length - 1];
    return last ? roundLabel(last.number, last.part) : '';
  }

  protected joinTarget(r: Round): string {
    const move = r.week?.joinPrevious;
    return move?.targetNumber ? roundLabel(move.targetNumber, move.targetPart) : '';
  }

  protected leaveTarget(r: Round): string {
    const move = r.week?.leave;
    return move?.targetNumber ? roundLabel(move.targetNumber, move.targetPart) : '';
  }

  /** Two lists in the same week: play this round as the next part of the previous one. */
  async joinPreviousWeek(r: Round): Promise<void> {
    const move = r.week?.joinPrevious;
    if (!move?.allowed) return;
    const ok = await this.confirmRegroup(
      'roundDetail.confirmJoinPrevious',
      'roundDetail.joinPrevious',
      roundLabel(r.number, r.part),
      this.joinTarget(r),
      move.renumberedRounds,
      move.requiresRecalculation,
    );
    if (!ok) return;
    this.regroup(this.api.joinPreviousWeek(r.id), 'roundDetail.joinedPrevious');
  }

  /** Undo of a grouping: the last part becomes a round of its own again. */
  async leaveWeek(r: Round): Promise<void> {
    const move = r.week?.leave;
    if (!move?.allowed) return;
    const ok = await this.confirmRegroup(
      'roundDetail.confirmLeaveWeek',
      'roundDetail.leaveWeek',
      roundLabel(r.number, r.part),
      this.leaveTarget(r),
      move.renumberedRounds,
      move.requiresRecalculation,
    );
    if (!ok) return;
    this.regroup(this.api.leaveWeek(r.id), 'roundDetail.leftWeek');
  }

  /**
   * Regrouping renumbers the rounds after it and, when a scored round is involved, replays the
   * season — everyone's absences, penalties and the standings can move, and links and messages
   * already sent to the group keep the old numbers. The dialog says all of that up front.
   */
  private confirmRegroup(
    messageKey: string,
    actionKey: string,
    from: string,
    to: string,
    renumbered: number,
    recalculates: boolean,
  ): Promise<boolean> {
    let message = this.translate.instant(messageKey, { from, to });
    if (renumbered > 1) {
      message +=
        ' ' + this.translate.instant('roundDetail.regroupRenumbers', { count: renumbered });
    }
    if (recalculates) {
      message += ' ' + this.translate.instant('roundDetail.regroupRecalc');
    }
    return this.confirm.ask(message, {
      title: this.translate.instant(actionKey),
      confirmText: this.translate.instant(actionKey),
    });
  }

  private regroup(request: Observable<Round>, successKey: string): void {
    this.regrouping.set(true);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.regrouping.set(false);
        this.after(successKey);
      },
      error: () => this.regrouping.set(false),
    });
  }

  private after(key: string): void {
    this.toast.success(this.translate.instant(key));
    this.load();
  }
}
