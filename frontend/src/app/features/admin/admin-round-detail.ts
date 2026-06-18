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
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin">Admin</a> ·
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        {{ 'roundDetail.crumb' | translate }}
      </div>
      <div class="d-flex align-items-center gap-2 flex-wrap">
        <h1 class="h4 fw-bold mb-0">{{ 'roundDetail.title' | translate }}</h1>
        @if (round(); as r) {
          @if (r.allowParticipantsToSubmitPredictions === false) {
            <span class="badge text-bg-secondary">{{
              'predictionSubmission.adminOnlyBadge' | translate
            }}</span>
          } @else {
            <span class="badge text-bg-success">{{
              'predictionSubmission.participantAppBadge' | translate
            }}</span>
          }
        }
      </div>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (round(); as r) {
      <div class="card mb-3">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h2 class="h5 fw-bold mb-0">
              {{ 'dashboard.round' | translate }} {{ r.number }}
              @if (r.title) {
                · {{ r.title }}
              }
            </h2>
            <app-round-status-badge [status]="r.status" />
          </div>

          <!-- Edit metadata -->
          @if (r.status === RoundStatus.Draft || r.status === RoundStatus.Published) {
            <form [formGroup]="form" (ngSubmit)="saveMeta()" class="row g-2 align-items-end mb-2">
              <div class="col-12 col-sm-4">
                <label class="form-label small mb-0">{{ 'roundDetail.number' | translate }}</label>
                <input type="number" min="1" class="form-control" formControlName="number" />
              </div>
              <div class="col-12 col-sm-5">
                <label class="form-label small mb-0">{{ 'roundDetail.name' | translate }}</label>
                <input class="form-control" formControlName="title" />
              </div>
              <div class="col-12 col-sm-3">
                <button class="btn btn-outline-secondary w-100" [disabled]="form.invalid">
                  {{ 'roundDetail.save' | translate }}
                </button>
              </div>
            </form>
          }

          <!-- Manage actions -->
          <div class="section-label mt-3 mb-2">{{ 'roundDetail.manage' | translate }}</div>
          <div class="row g-2">
            <div class="col-12 col-md-6">
              <a class="action-card h-100" [routerLink]="['/admin/rounds', r.id, 'matches']">
                <span class="icon-tile icon-tile--blue"><app-icon name="goal" [size]="20" /></span>
                <div class="action-card__title">{{ 'roundDetail.manageGames' | translate }}</div>
                <span class="action-card__arrow"><app-icon name="arrow-right" [size]="18" /></span>
              </a>
            </div>
            <div class="col-12 col-md-6">
              <a
                class="action-card h-100"
                [routerLink]="['/admin/rounds', r.id, 'manual-predictions']"
              >
                <span class="icon-tile icon-tile--violet"
                  ><app-icon name="pencil" [size]="20"
                /></span>
                <div class="action-card__title">
                  {{ 'roundDetail.registerPredictions' | translate }}
                </div>
                <span class="action-card__arrow"><app-icon name="arrow-right" [size]="18" /></span>
              </a>
            </div>
            <div class="col-12 col-md-6">
              <a
                class="action-card h-100"
                [routerLink]="['/admin/rounds', r.id, 'import-predictions']"
              >
                <span class="icon-tile icon-tile--amber"
                  ><app-icon name="image" [size]="20"
                /></span>
                <div class="action-card__title">{{ 'roundDetail.importImage' | translate }}</div>
                <span class="action-card__arrow"><app-icon name="arrow-right" [size]="18" /></span>
              </a>
            </div>
            <div class="col-12 col-md-6">
              <a class="action-card h-100" [routerLink]="['/admin/rounds', r.id, 'scout']">
                <span class="icon-tile icon-tile--teal"
                  ><app-icon name="search" [size]="20"
                /></span>
                <div class="action-card__title">{{ 'scout.title' | translate }}</div>
                <span class="action-card__arrow"><app-icon name="arrow-right" [size]="18" /></span>
              </a>
            </div>

            @if (r.status === RoundStatus.Published || r.status === RoundStatus.Locked) {
              <div class="col-12 col-md-6">
                <button
                  type="button"
                  class="action-card h-100 w-100 text-start"
                  (click)="refreshResults(r)"
                  [disabled]="refreshing()"
                >
                  <span class="icon-tile icon-tile--blue">
                    @if (refreshing()) {
                      <span class="spinner-border spinner-border-sm"></span>
                    } @else {
                      <app-icon name="refresh-cw" [size]="20" />
                    }
                  </span>
                  <div class="action-card__title">{{ 'results.refresh' | translate }}</div>
                  <span class="action-card__arrow"
                    ><app-icon name="arrow-right" [size]="18"
                  /></span>
                </button>
              </div>
              <div class="col-12 col-md-6">
                <a
                  class="action-card h-100"
                  [routerLink]="['/rounds', r.id, 'temporary-standings']"
                >
                  <span class="icon-tile icon-tile--green"
                    ><app-icon name="chart-column" [size]="20"
                  /></span>
                  <div class="action-card__title">
                    {{ 'temporaryStandings.title' | translate }}
                  </div>
                  <span class="action-card__arrow"
                    ><app-icon name="arrow-right" [size]="18"
                  /></span>
                </a>
              </div>
            }
          </div>

          <!-- Round lifecycle -->
          <div class="section-label mt-3 mb-2">{{ 'roundDetail.lifecycle' | translate }}</div>
          <app-round-stepper [status]="r.status" class="d-block mb-3" />

          <!-- Next step: one guided primary action per state -->
          @if (r.status === RoundStatus.Draft) {
            <div class="d-grid gap-2">
              @if (r.matches.length === 0) {
                <div class="alert alert-warning py-2 mb-0">
                  {{ 'roundDetail.needMatchesToPublish' | translate }}
                  <a [routerLink]="['/admin/rounds', r.id, 'matches']">{{
                    'roundDetail.manageGames' | translate
                  }}</a>
                </div>
              }
              <button
                class="btn btn-success btn-lg"
                (click)="publish(r)"
                [disabled]="r.matches.length === 0"
              >
                {{ 'roundDetail.publish' | translate }}
              </button>
            </div>
          }

          @if (r.status === RoundStatus.Published) {
            <div class="d-grid gap-2">
              <button class="btn btn-warning btn-lg" (click)="lock(r)">
                <app-icon name="lock" [size]="18" /> {{ 'roundDetail.lock' | translate }}
              </button>
              <p class="text-muted small mb-0">{{ 'roundDetail.lockHint' | translate }}</p>
            </div>
          }

          @if (r.status === RoundStatus.Locked || r.status === RoundStatus.Scored) {
            <div class="section-label mt-1 mb-2">{{ 'adminResults.title' | translate }}</div>
            <app-round-results-editor [matches]="matches()" (saved)="load()" />

            <div class="d-grid gap-2 mt-3">
              @if (r.status === RoundStatus.Locked && !resultsComplete()) {
                <div class="alert alert-info py-2 mb-0">
                  {{
                    'roundDetail.resultsMissing'
                      | translate: { count: missingResults(), total: r.matches.length }
                  }}
                </div>
              }
              <button
                class="btn btn-primary btn-lg"
                (click)="score(r)"
                [disabled]="finalizing() || (r.status === RoundStatus.Locked && !resultsComplete())"
              >
                @if (finalizing()) {
                  <span class="spinner-border spinner-border-sm me-2"></span>
                }
                {{
                  (r.status === RoundStatus.Scored
                    ? 'roundDetail.recalculate'
                    : 'roundDetail.finalize'
                  ) | translate
                }}
              </button>
              @if (r.status === RoundStatus.Scored) {
                <button class="btn btn-outline-warning" (click)="reopen(r)">
                  <app-icon name="undo-2" [size]="16" /> {{ 'roundDetail.reopen' | translate }}
                </button>
              }
            </div>
          }

          <!-- Cancel: secondary, destructive -->
          @if (r.status !== RoundStatus.Scored && r.status !== RoundStatus.Cancelled) {
            <div class="d-grid mt-3">
              <button class="btn btn-outline-danger" (click)="cancel(r)">
                {{ 'roundDetail.cancelRound' | translate }}
              </button>
            </div>
          }
        </div>
      </div>

      <!-- Results refresh summary -->
      @if (refreshSummary(); as rs) {
        <div class="card mb-3 border-info">
          <div class="card-body py-2">
            <div class="d-flex flex-wrap gap-3 small">
              <span
                ><app-icon name="circle-check" [size]="14" /> {{ 'results.finished' | translate }}:
                <b>{{ rs.finishedMatches }}</b></span
              >
              <span
                ><app-icon name="play" [size]="14" /> {{ 'results.inProgress' | translate }}:
                <b>{{ rs.inProgressMatches }}</b></span
              >
              <span
                ><app-icon name="hourglass" [size]="14" /> {{ 'results.notStarted' | translate }}:
                <b>{{ rs.notStartedMatches }}</b></span
              >
              <span
                ><app-icon name="refresh-cw" [size]="14" /> {{ 'results.updated' | translate }}:
                <b>{{ rs.updatedMatches }}</b></span
              >
            </div>
          </div>
        </div>
      }

      <!-- Copy-ready closing + group messages -->
      <app-admin-round-messages
        [closingMessage]="closing()"
        [groupMessage]="r.matches.length > 0 ? message(r) : ''"
      />

      <!-- Matches -->
      <h2 class="h6 fw-bold mb-2">
        {{ 'roundDetail.games' | translate }} ({{ r.matches.length }})
      </h2>
      <app-match-list [matches]="r.matches" />
    }
  `,
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
