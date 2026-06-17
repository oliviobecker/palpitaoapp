import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { RoundStatus } from '../../core/models/enums';
import { Round } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { GroupContextService } from '../../core/services/group-context.service';
import { RoundsService } from '../../core/services/rounds.service';
import { StandingsService } from '../../core/services/standings.service';
import { RefreshResultsResponse } from '../../core/models/models';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { Loading } from '../../shared/components/loading/loading';
import { MultiplierBadge } from '../../shared/components/multiplier-badge/multiplier-badge';
import { RoundStatusBadge } from '../../shared/components/round-status-badge/round-status-badge';
import { copyToClipboard } from '../../shared/utils/clipboard.util';
import { computeMultiplier, isClassic, isLeagueOne } from '../../shared/utils/match.util';
import { buildRoundMessage } from '../../shared/utils/round-message.util';
import { buildClosingMessage } from '../../shared/utils/closing-message.util';

@Component({
  selector: 'app-admin-round-detail',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    TranslatePipe,
    CompetitionBadge,
    Loading,
    MultiplierBadge,
    RoundStatusBadge,
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
                <span class="icon-tile icon-tile--blue">⚽</span>
                <div class="action-card__title">{{ 'roundDetail.manageGames' | translate }}</div>
                <span class="action-card__arrow">→</span>
              </a>
            </div>
            <div class="col-12 col-md-6">
              <a
                class="action-card h-100"
                [routerLink]="['/admin/rounds', r.id, 'manual-predictions']"
              >
                <span class="icon-tile icon-tile--violet">✍️</span>
                <div class="action-card__title">
                  {{ 'roundDetail.registerPredictions' | translate }}
                </div>
                <span class="action-card__arrow">→</span>
              </a>
            </div>
            <div class="col-12 col-md-6">
              <a
                class="action-card h-100"
                [routerLink]="['/admin/rounds', r.id, 'import-predictions']"
              >
                <span class="icon-tile icon-tile--amber">🖼️</span>
                <div class="action-card__title">{{ 'roundDetail.importImage' | translate }}</div>
                <span class="action-card__arrow">→</span>
              </a>
            </div>
            <div class="col-12 col-md-6">
              <a class="action-card h-100" [routerLink]="['/admin/rounds', r.id, 'scout']">
                <span class="icon-tile icon-tile--teal">🔎</span>
                <div class="action-card__title">{{ 'scout.title' | translate }}</div>
                <span class="action-card__arrow">→</span>
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
                      🔄
                    }
                  </span>
                  <div class="action-card__title">{{ 'results.refresh' | translate }}</div>
                  <span class="action-card__arrow">→</span>
                </button>
              </div>
              <div class="col-12 col-md-6">
                <a
                  class="action-card h-100"
                  [routerLink]="['/rounds', r.id, 'temporary-standings']"
                >
                  <span class="icon-tile icon-tile--green">📊</span>
                  <div class="action-card__title">
                    {{ 'temporaryStandings.title' | translate }}
                  </div>
                  <span class="action-card__arrow">→</span>
                </a>
              </div>
            }
            @if (r.status === RoundStatus.Locked || r.status === RoundStatus.Scored) {
              <div class="col-12 col-md-6">
                <a class="action-card h-100" [routerLink]="['/admin/rounds', r.id, 'results']">
                  <span class="icon-tile icon-tile--violet">📋</span>
                  <div class="action-card__title">{{ 'roundDetail.results' | translate }}</div>
                  <span class="action-card__arrow">→</span>
                </a>
              </div>
            }
          </div>

          <!-- Round lifecycle -->
          <div class="section-label mt-3 mb-2">{{ 'roundDetail.lifecycle' | translate }}</div>
          <div class="d-grid gap-2">
            @if (r.status === RoundStatus.Draft) {
              <button class="btn btn-success" (click)="publish(r)">
                {{ 'roundDetail.publish' | translate }}
              </button>
            }
            @if (r.status === RoundStatus.Published) {
              <button class="btn btn-outline-warning" (click)="lock(r)">
                {{ 'roundDetail.lock' | translate }}
              </button>
            }
            @if (
              r.status === RoundStatus.Published ||
              r.status === RoundStatus.Locked ||
              r.status === RoundStatus.Scored
            ) {
              <button class="btn btn-primary" (click)="score(r)" [disabled]="finalizing()">
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
            }
            @if (r.status !== RoundStatus.Scored && r.status !== RoundStatus.Cancelled) {
              <button class="btn btn-outline-danger" (click)="cancel(r)">
                {{ 'roundDetail.cancelRound' | translate }}
              </button>
            }
          </div>
        </div>
      </div>

      <!-- Results refresh summary -->
      @if (refreshSummary(); as rs) {
        <div class="card mb-3 border-info">
          <div class="card-body py-2">
            <div class="d-flex flex-wrap gap-3 small">
              <span
                >✅ {{ 'results.finished' | translate }}: <b>{{ rs.finishedMatches }}</b></span
              >
              <span
                >▶️ {{ 'results.inProgress' | translate }}: <b>{{ rs.inProgressMatches }}</b></span
              >
              <span
                >⏳ {{ 'results.notStarted' | translate }}: <b>{{ rs.notStartedMatches }}</b></span
              >
              <span
                >🔄 {{ 'results.updated' | translate }}: <b>{{ rs.updatedMatches }}</b></span
              >
            </div>
          </div>
        </div>
      }

      <!-- Closing message (copy-ready, once the round is finalized) -->
      @if (r.status === RoundStatus.Scored && closing()) {
        <div class="card mb-3 border-success">
          <div class="card-body">
            <div class="d-flex justify-content-between align-items-center mb-2">
              <h2 class="h6 fw-bold mb-0">🏁 {{ 'roundDetail.closingMessage' | translate }}</h2>
              <button class="btn btn-sm btn-success" type="button" (click)="copyClosing()">
                📋 {{ 'roundDetail.copy' | translate }}
              </button>
            </div>
            <pre
              class="small mb-0 p-2 bg-light rounded border"
              style="white-space: pre-wrap; word-break: break-word"
              >{{ closing() }}</pre
            >
          </div>
        </div>
      }

      <!-- Group message (copy-ready) -->
      @if (r.matches.length > 0) {
        <div class="card mb-3">
          <div class="card-body">
            <div class="d-flex justify-content-between align-items-center mb-2">
              <h2 class="h6 fw-bold mb-0">{{ 'roundDetail.groupMessage' | translate }}</h2>
              <button class="btn btn-sm btn-primary" type="button" (click)="copyMessage(r)">
                📋 {{ 'roundDetail.copy' | translate }}
              </button>
            </div>
            <pre
              class="small mb-0 p-2 bg-light rounded border"
              style="white-space: pre-wrap; word-break: break-word"
              >{{ message(r) }}</pre
            >
          </div>
        </div>
      }

      <!-- Matches -->
      <h2 class="h6 fw-bold mb-2">
        {{ 'roundDetail.games' | translate }} ({{ r.matches.length }})
      </h2>
      <div class="vstack gap-2">
        @for (m of r.matches; track m.id) {
          <div
            class="card"
            [class.border-primary]="classic(m)"
            [class.border-warning]="leagueOne(m)"
          >
            <div class="card-body py-2 px-3">
              <div class="d-flex flex-wrap gap-2 align-items-center mb-1">
                <app-competition-badge [competition]="m.competition" />
                <app-multiplier-badge [multiplier]="multiplier(m)" />
                @if (classic(m)) {
                  <span class="badge text-bg-primary">{{ 'predictions.classic' | translate }}</span>
                }
                @if (leagueOne(m)) {
                  <span class="badge text-bg-warning">{{
                    'predictions.leagueOne' | translate
                  }}</span>
                }
                <small class="text-muted ms-auto">{{ m.startsAt | date: 'dd/MM HH:mm' }}</small>
              </div>
              <div class="fw-semibold">
                {{ m.homeTeamName }} <span class="text-muted">x</span> {{ m.awayTeamName }}
              </div>
            </div>
          </div>
        }
      </div>
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

  protected readonly RoundStatus = RoundStatus;
  protected readonly multiplier = computeMultiplier;
  protected readonly classic = isClassic;
  protected readonly leagueOne = isLeagueOne;

  protected readonly loading = signal(true);
  protected readonly finalizing = signal(false);
  protected readonly refreshing = signal(false);
  protected readonly refreshSummary = signal<RefreshResultsResponse | null>(null);
  protected readonly round = signal<Round | null>(null);
  protected readonly closing = signal('');
  private id = '';

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
    this.api.getById(this.id).subscribe({
      next: (r) => {
        this.round.set(r);
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
    }).subscribe({
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
    this.api.update(this.id, { number, title: title || null }).subscribe({
      next: () => this.after('roundDetail.updated'),
    });
  }

  publish(r: Round): void {
    this.api.publish(r.id).subscribe({ next: () => this.after('roundDetail.published') });
  }

  lock(r: Round): void {
    this.api.lock(r.id).subscribe({ next: () => this.after('roundDetail.locked') });
  }

  score(r: Round): void {
    // "Finalizar" from a published round locks it first, then scores; an
    // already-locked round just scores; re-running on a scored round recalculates.
    this.finalizing.set(true);
    const finish = () => {
      this.finalizing.set(false);
      this.after(r.status === RoundStatus.Scored ? 'roundDetail.scored' : 'roundDetail.finalized');
    };
    const onError = () => this.finalizing.set(false);

    if (r.status === RoundStatus.Published) {
      this.api.lock(r.id).subscribe({
        next: () => this.api.score(r.id).subscribe({ next: finish, error: onError }),
        error: onError,
      });
    } else {
      this.api.score(r.id).subscribe({ next: finish, error: onError });
    }
  }

  async cancel(r: Round): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('roundDetail.confirmCancel'), {
      title: this.translate.instant('roundDetail.cancelRound'),
      confirmText: this.translate.instant('roundDetail.cancelRound'),
      danger: true,
    });
    if (ok) {
      this.api.cancel(r.id).subscribe({ next: () => this.after('roundDetail.cancelled') });
    }
  }

  refreshResults(round: Round): void {
    this.refreshing.set(true);
    this.adminApi.refreshResults(round.id).subscribe({
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

  async copyMessage(round: Round): Promise<void> {
    const ok = await copyToClipboard(buildRoundMessage(round, this.group.groupName() ?? ''));
    this.toast.success(
      this.translate.instant(ok ? 'roundDetail.copied' : 'roundDetail.copyFailed'),
    );
  }

  async copyClosing(): Promise<void> {
    const ok = await copyToClipboard(this.closing());
    this.toast.success(
      this.translate.instant(ok ? 'roundDetail.copied' : 'roundDetail.copyFailed'),
    );
  }

  private after(key: string): void {
    this.toast.success(this.translate.instant(key));
    this.load();
  }
}
