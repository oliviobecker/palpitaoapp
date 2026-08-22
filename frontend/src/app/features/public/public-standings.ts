import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Lang, LanguageService } from '../../core/i18n/language.service';
import { ScoreCategory } from '../../core/models/enums';
import {
  PublicMatchScore,
  PublicParticipantScore,
  PublicRound,
  PublicRoundSummary,
  PublicSeason,
  PublicStandingRow,
  RoundResultMatch,
} from '../../core/models/models';
import { PublicStandingsService } from '../../core/services/public-standings.service';
import { ThemeService } from '../../core/theme/theme.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { MultiplierBadge } from '../../shared/components/multiplier-badge/multiplier-badge';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';
import { avatarColor, initials } from '../../shared/utils/avatar.util';
import { phaseLabel } from '../../shared/utils/match.util';
import { shortTeamName } from '../../shared/utils/team-name.util';

type Tab = 'overall' | 'round';
type Cut = 'participant' | 'match';

/**
 * Public, read-only standings and scoring audit, reached by a season's public key and no
 * account at all. The audit is not a separate screen: a participant's row expands in
 * place, so the reader never loses sight of who is around them while checking a number.
 *
 * Lives outside the Shell — no navbar, no group context, no session.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-public-standings',
  imports: [
    TranslatePipe,
    CompetitionBadge,
    EmptyState,
    ErrorState,
    Icon,
    MultiplierBadge,
    SkeletonList,
  ],
  styles: [
    `
      .public-wrap {
        max-width: 720px;
        margin-inline: auto;
        padding: 1rem 0.75rem 3rem;
      }
      /* Desktop gets room for the extra columns instead of a phone-width column
         stranded in the middle of a monitor. */
      @media (min-width: 768px) {
        .public-wrap {
          max-width: 960px;
        }
      }
      .calc {
        font-variant-numeric: tabular-nums;
      }
      .match-row:last-child {
        border-bottom: 0 !important;
      }
      /* The accordion header is a bare button over a card body: without this it gives no
         sign at all that it can be pressed. */
      .row-toggle {
        transition: background-color 0.15s ease;
      }
      .row-toggle:hover,
      .row-toggle:focus-visible {
        background-color: var(--surface-2) !important;
      }
    `,
  ],
  template: `
    <div class="public-wrap">
      <header class="d-flex justify-content-between align-items-start gap-2 mb-3">
        <div>
          <div class="d-flex align-items-center gap-2">
            <span aria-hidden="true">⚽</span>
            <span class="fw-bold">{{ season()?.groupName || ('app.name' | translate) }}</span>
          </div>
          @if (season(); as s) {
            <div class="small text-muted">
              {{ s.seasonName }} · {{ 'publicStandings.readOnly' | translate }}
            </div>
          }
        </div>
        <div class="d-flex gap-1">
          <div
            class="btn-group btn-group-sm"
            role="group"
            [attr.aria-label]="'language.label' | translate"
          >
            <button
              type="button"
              class="btn btn-outline-secondary"
              [class.active]="language.current() === 'pt-BR'"
              (click)="setLanguage('pt-BR')"
            >
              PT
            </button>
            <button
              type="button"
              class="btn btn-outline-secondary"
              [class.active]="language.current() === 'en-US'"
              (click)="setLanguage('en-US')"
            >
              EN
            </button>
          </div>
          <button
            type="button"
            class="btn btn-sm btn-outline-secondary"
            (click)="theme.toggle()"
            [attr.aria-label]="'theme.toggle' | translate"
          >
            <app-icon [name]="theme.current() === 'dark' ? 'sun' : 'moon'" [size]="16" />
          </button>
        </div>
      </header>

      @if (loading()) {
        <app-skeleton-list [count]="5" />
      } @else if (invalidKey()) {
        <app-empty-state icon="triangle-alert" [message]="'publicStandings.notFound' | translate" />
      } @else if (error()) {
        <app-error-state (retry)="load()" />
      } @else if (season(); as s) {
        <div class="btn-group w-100 mb-3" role="group">
          <button
            type="button"
            class="btn btn-outline-primary"
            [class.active]="tab() === 'overall'"
            (click)="selectTab('overall')"
          >
            {{ 'publicStandings.overall' | translate }}
          </button>
          <button
            type="button"
            class="btn btn-outline-primary"
            [class.active]="tab() === 'round'"
            [disabled]="s.rounds.length === 0"
            (click)="selectTab('round')"
          >
            {{ 'publicStandings.byRound' | translate }}
          </button>
        </div>

        @if (tab() === 'round') {
          @if (missingRound(); as wanted) {
            <div class="alert alert-secondary py-2 small" role="status">
              {{ 'publicStandings.roundUnavailable' | translate: { n: wanted } }}
            </div>
          }
          <div class="mb-3">
            <label class="form-label small" for="round-select">{{
              'publicStandings.round' | translate
            }}</label>
            <select
              id="round-select"
              class="form-select"
              [value]="roundNumber() ?? ''"
              (change)="pickRound($any($event.target).value)"
            >
              @for (r of s.rounds; track r.number) {
                <option [value]="r.number">
                  {{ 'publicStandings.roundN' | translate: { n: r.number } }}
                  @if (r.title) {
                    · {{ r.title }}
                  }
                  @if (roundDates(r); as dates) {
                    · {{ dates }}
                  }
                  @if (!r.isScored) {
                    · {{ 'publicStandings.partial' | translate }}
                  }
                </option>
              }
            </select>
          </div>
        }

        @if (tab() === 'overall') {
          @if (standingsError()) {
            <app-error-state (retry)="loadStandings()" />
          } @else if (standings().length === 0) {
            <app-empty-state icon="trophy" [message]="'publicStandings.noStandings' | translate" />
          } @else {
            @if (podium().length === 3) {
              <div class="podium mb-3">
                @for (p of podium(); track p.userId) {
                  <div
                    class="podium__slot podium__slot--{{ p.position }}"
                    [class.podium__slot--me]="p.userId === meId()"
                  >
                    <span class="podium__rank">{{ p.position }}</span>
                    <span class="podium__avatar" [style.background]="avatarColor(p.name)">{{
                      initials(p.name)
                    }}</span>
                    <div class="podium__name text-truncate">{{ p.name }}</div>
                    <div class="podium__pts">
                      {{ p.totalPoints }} <small>{{ 'dashboard.pts' | translate }}</small>
                    </div>
                  </div>
                }
              </div>
            }

            <div class="mb-2">
              <input
                type="search"
                class="form-control form-control-sm"
                [value]="filter()"
                (input)="filter.set($any($event.target).value)"
                [placeholder]="'publicStandings.findName' | translate"
                [attr.aria-label]="'publicStandings.findName' | translate"
              />
              @if (filter().trim()) {
                <div class="form-text">
                  {{
                    'publicStandings.showingOf'
                      | translate: { shown: rows().length, total: standings().length }
                  }}
                </div>
              }
            </div>

            <!-- Column labels: desktop only, where there is room for the numbers the phone
                 keeps inside the expansion. -->
            <div class="d-none d-md-flex small text-muted px-3 pb-1">
              <span class="ms-auto d-flex gap-3 text-end">
                <span style="min-width: 3.5rem">{{ 'standings.rounds' | translate }}</span>
                <span style="min-width: 3.5rem">{{ 'standings.absences' | translate }}</span>
                <span style="min-width: 3.5rem">{{ 'publicStandings.diff' | translate }}</span>
                <span style="min-width: 3.5rem">{{ 'standings.points' | translate }}</span>
                <span style="width: 1rem"></span>
              </span>
            </div>

            @if (rows().length === 0) {
              <app-empty-state icon="search" [message]="'publicStandings.noMatch' | translate" />
            } @else {
              <div class="vstack gap-2">
                @for (row of rows(); track row.userId) {
                  <div
                    class="card"
                    [class.border-danger]="row.isEliminated"
                    [class.border-primary]="row.userId === meId()"
                  >
                    <button
                      type="button"
                      class="row-toggle card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
                      [attr.aria-expanded]="isOpen(row.userId)"
                      [attr.aria-controls]="'overall-' + row.userId"
                      (click)="toggle(row.userId)"
                    >
                      <span class="d-flex align-items-center gap-2" style="min-width: 0">
                        <span class="text-muted small" style="min-width: 1.5rem">{{
                          row.position
                        }}</span>
                        <span class="rank-avatar" [style.background]="avatarColor(row.name)">{{
                          initials(row.name)
                        }}</span>
                        <span class="fw-semibold text-truncate">{{ row.name }}</span>
                        @if (row.userId === meId()) {
                          <span class="badge text-bg-primary">{{ 'common.you' | translate }}</span>
                        }
                        @if (row.isEliminated) {
                          <span class="badge text-bg-danger">{{
                            'standings.eliminated' | translate
                          }}</span>
                        }
                      </span>
                      <span class="d-flex align-items-center gap-3 flex-shrink-0">
                        <span class="d-none d-md-flex gap-3 small text-muted calc text-end">
                          <span style="min-width: 3.5rem">{{ row.playedRounds }}</span>
                          <span style="min-width: 3.5rem">{{ row.absenceCount }}</span>
                          <span style="min-width: 3.5rem">{{
                            row.toLeader > 0 ? '−' + row.toLeader : '—'
                          }}</span>
                        </span>
                        <span class="h6 fw-bold mb-0 calc text-end" style="min-width: 3.5rem">{{
                          row.totalPoints
                        }}</span>
                        <app-icon
                          [name]="isOpen(row.userId) ? 'chevron-down' : 'chevron-right'"
                          [size]="16"
                        />
                      </span>
                    </button>

                    @if (isOpen(row.userId)) {
                      <div class="card-body pt-0 px-3 small" [id]="'overall-' + row.userId">
                        <div class="d-flex flex-wrap gap-3 text-muted">
                          <span>{{ 'standings.rounds' | translate }}: {{ row.playedRounds }}</span>
                          <span
                            >{{ 'standings.absences' | translate }}: {{ row.absenceCount }}</span
                          >
                          @if (row.penaltyPoints > 0) {
                            <span class="text-danger"
                              >{{ 'standings.penalties' | translate }}: −{{
                                row.penaltyPoints
                              }}</span
                            >
                          }
                        </div>
                        @if (row.toLeader > 0) {
                          <div class="text-muted mt-1">
                            {{ 'publicStandings.behindLeader' | translate: { n: row.toLeader } }}
                            @if (row.above) {
                              &middot;
                              {{
                                'publicStandings.behindAbove'
                                  | translate: { n: row.toAbove, name: row.above }
                              }}
                            }
                          </div>
                        }
                        @if (row.rounds.length > 0) {
                          <div class="mt-2">
                            <div class="text-muted mb-1">
                              {{ 'publicStandings.roundHistory' | translate }}
                            </div>
                            <div class="d-flex flex-wrap gap-1">
                              @for (h of row.rounds; track h.number) {
                                <button
                                  type="button"
                                  class="btn btn-sm btn-outline-secondary py-0 px-2 calc"
                                  [class.border-warning]="h.flavioRuleApplied"
                                  [class.text-muted]="h.wasAbsent"
                                  [attr.title]="
                                    'publicStandings.roundN' | translate: { n: h.number }
                                  "
                                  (click)="auditParticipant(row.userId, h.number)"
                                >
                                  <span class="text-muted">{{ h.number }}</span>
                                  <span class="text-muted mx-1">·</span>
                                  <span class="fw-semibold">{{ h.points }}</span>
                                  @if (h.wasAbsent) {
                                    <app-icon name="ban" [size]="12" class="ms-1" />
                                  }
                                </button>
                              }
                            </div>
                          </div>
                        }
                        <div class="d-flex flex-wrap gap-2 mt-2">
                          @if (s.rounds.length > 0) {
                            <button
                              type="button"
                              class="btn btn-sm btn-outline-secondary"
                              (click)="auditParticipant(row.userId)"
                            >
                              {{ 'publicStandings.seeRoundDetail' | translate }}
                            </button>
                          }
                          <button
                            type="button"
                            class="btn btn-sm"
                            [class.btn-primary]="row.userId === meId()"
                            [class.btn-outline-secondary]="row.userId !== meId()"
                            (click)="markMe(row.userId)"
                          >
                            {{
                              (row.userId === meId()
                                ? 'publicStandings.unmarkMe'
                                : 'publicStandings.markMe'
                              ) | translate
                            }}
                          </button>
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            }
          }
        } @else {
          @if (roundLoading()) {
            <app-skeleton-list [count]="4" />
          } @else if (roundError()) {
            <app-error-state (retry)="reloadRound()" />
          } @else if (round(); as r) {
            @if (r.isPartial) {
              <div class="alert alert-warning py-2 small d-flex align-items-center gap-2">
                <app-icon name="timer" [size]="16" />
                <span>
                  {{ 'publicStandings.partialNotice' | translate }}
                  ({{
                    'publicStandings.computedOf'
                      | translate: { done: r.computedMatches, left: r.remainingMatches }
                  }})
                </span>
              </div>
            }

            @if (r.participants.length === 0) {
              <app-empty-state
                icon="hourglass"
                [message]="'publicStandings.noScores' | translate"
              />
            } @else {
              <!-- Two ways to read the same round. "By participant" answers "how did I do?";
                   "by match" answers "who got the Arsenal game right?", which is how the
                   argument in the group actually starts. -->
              <div class="d-flex justify-content-between align-items-center gap-2 mb-2 flex-wrap">
                <div class="btn-group btn-group-sm" role="group">
                  <button
                    type="button"
                    class="btn btn-outline-secondary"
                    [class.active]="cut() === 'participant'"
                    (click)="cut.set('participant')"
                  >
                    {{ 'publicStandings.byParticipant' | translate }}
                  </button>
                  <button
                    type="button"
                    class="btn btn-outline-secondary"
                    [class.active]="cut() === 'match'"
                    (click)="cut.set('match')"
                  >
                    {{ 'publicStandings.byMatch' | translate }}
                  </button>
                </div>
                <button
                  type="button"
                  class="btn btn-sm btn-link text-decoration-none p-0"
                  (click)="toggleAll(r)"
                >
                  {{
                    (allOpen(r) ? 'publicStandings.collapseAll' : 'publicStandings.expandAll')
                      | translate
                  }}
                </button>
              </div>

              @if (cut() === 'participant') {
                <div class="vstack gap-2">
                  @for (p of r.participants; track p.userId) {
                    <div class="card" [class.border-primary]="p.userId === meId()">
                      <button
                        type="button"
                        class="row-toggle card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
                        [attr.aria-expanded]="isOpen(p.userId)"
                        [attr.aria-controls]="'round-' + p.userId"
                        (click)="toggle(p.userId)"
                      >
                        <span class="fw-semibold d-flex align-items-center gap-2 flex-wrap">
                          <span class="rank-avatar" [style.background]="avatarColor(p.name)">{{
                            initials(p.name)
                          }}</span>
                          {{ p.name }}
                          @if (p.wasAbsent) {
                            <span class="badge text-bg-secondary">{{
                              'results.absent' | translate
                            }}</span>
                          }
                          @if (p.flavioRuleApplied) {
                            <span class="badge text-bg-warning">{{
                              'results.flavioApplied' | translate
                            }}</span>
                          }
                        </span>
                        <span class="d-flex align-items-center gap-2">
                          <span class="h6 fw-bold mb-0 calc">{{ p.finalPoints }}</span>
                          <app-icon
                            [name]="isOpen(p.userId) ? 'chevron-down' : 'chevron-right'"
                            [size]="16"
                          />
                        </span>
                      </button>

                      @if (isOpen(p.userId)) {
                        <div class="card-body pt-0 px-3" [id]="'round-' + p.userId">
                          <div class="vstack gap-1">
                            @for (m of r.matches; track m.roundMatchId) {
                              <div
                                class="match-row d-flex justify-content-between align-items-center small border-bottom py-1 gap-2"
                                [class.opacity-50]="!hasResult(m)"
                              >
                                <span class="d-flex flex-column">
                                  <span class="fw-semibold"
                                    >{{ short(m.homeTeamName) }}
                                    <span class="calc"
                                      >{{ m.homeScore ?? '–' }} × {{ m.awayScore ?? '–' }}</span
                                    >
                                    {{ short(m.awayTeamName) }}</span
                                  >
                                  <span class="d-flex align-items-center gap-1 flex-wrap">
                                    <app-competition-badge [competition]="m.competition" />
                                    <app-multiplier-badge [multiplier]="m.multiplier" />
                                    @if (m.isClassic) {
                                      <span class="badge text-bg-primary">{{
                                        'predictions.classic' | translate
                                      }}</span>
                                    }
                                    @if (m.isManualMultiplier) {
                                      <span class="badge text-bg-secondary">{{
                                        'results.manualMultiplier' | translate
                                      }}</span>
                                    }
                                    @if (phaseLabel(m.phase); as ph) {
                                      <span class="text-muted">{{ ph }}</span>
                                    }
                                  </span>
                                </span>

                                <span class="text-end flex-shrink-0">
                                  @if (score(p, m.roundMatchId); as sc) {
                                    <span class="d-block text-muted text-nowrap"
                                      >{{ 'publicStandings.prediction' | translate }}
                                      <span class="calc text-nowrap"
                                        >{{ sc.predictedHomeScore ?? '–' }} ×
                                        {{ sc.predictedAwayScore ?? '–' }}</span
                                      ></span
                                    >
                                    <span class="d-block text-body">{{
                                      categoryLabel(sc.scoreCategory) | translate
                                    }}</span>
                                    <span class="text-muted calc"
                                      >{{ sc.basePoints }} × {{ sc.multiplier }} =</span
                                    >
                                    <span
                                      class="badge ms-1"
                                      [class.text-bg-success]="sc.finalPoints > 0"
                                      [class.text-bg-secondary]="sc.finalPoints === 0"
                                      >+{{ sc.finalPoints }}</span
                                    >
                                  } @else {
                                    <span class="text-muted text-nowrap">{{
                                      (hasResult(m)
                                        ? 'publicStandings.noPrediction'
                                        : 'publicStandings.awaiting'
                                      ) | translate
                                    }}</span>
                                  }
                                </span>
                              </div>
                            }
                          </div>

                          <div class="d-flex flex-wrap gap-3 small text-muted mt-2">
                            @if (p.wasAbsent) {
                              <span>{{ 'publicStandings.absentFooter' | translate }}</span>
                            }
                            @if (p.flavioRuleApplied) {
                              <span
                                >{{ 'publicStandings.gross' | translate }}
                                <span class="calc">{{ p.grossPoints }}</span> ·
                                {{ 'results.flavioApplied' | translate }}
                                <span class="calc">−{{ p.grossPoints - p.finalPoints }}</span></span
                              >
                            }
                            @if (p.penaltyPoints > 0) {
                              <span class="text-danger"
                                >{{ 'standings.penalties' | translate }}
                                <span class="calc">−{{ p.penaltyPoints }}</span></span
                              >
                            }
                            <span
                              >{{ 'publicStandings.roundTotal' | translate }}
                              <span class="calc fw-semibold">{{ p.finalPoints }}</span></span
                            >
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              } @else {
                <div class="vstack gap-2">
                  @for (row of byMatch(); track row.match.roundMatchId) {
                    <div class="card">
                      <button
                        type="button"
                        class="row-toggle card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
                        [attr.aria-expanded]="isMatchOpen(row.match.roundMatchId)"
                        [attr.aria-controls]="'match-' + row.match.roundMatchId"
                        (click)="toggleMatch(row.match.roundMatchId)"
                      >
                        <span class="d-flex flex-column" style="min-width: 0">
                          <span class="fw-semibold"
                            >{{ short(row.match.homeTeamName) }}
                            <span class="calc"
                              >{{ row.match.homeScore ?? '–' }} ×
                              {{ row.match.awayScore ?? '–' }}</span
                            >
                            {{ short(row.match.awayTeamName) }}</span
                          >
                          <span class="d-flex align-items-center gap-1 flex-wrap small">
                            <app-competition-badge [competition]="row.match.competition" />
                            <app-multiplier-badge [multiplier]="row.match.multiplier" />
                            @if (row.match.isClassic) {
                              <span class="badge text-bg-primary">{{
                                'predictions.classic' | translate
                              }}</span>
                            }
                            @if (phaseLabel(row.match.phase); as ph) {
                              <span class="text-muted">{{ ph }}</span>
                            }
                          </span>
                        </span>
                        <span class="d-flex align-items-center gap-2 flex-shrink-0">
                          <span class="small text-muted text-nowrap">{{
                            'publicStandings.hits' | translate: { n: row.hits }
                          }}</span>
                          <app-icon
                            [name]="
                              isMatchOpen(row.match.roundMatchId) ? 'chevron-down' : 'chevron-right'
                            "
                            [size]="16"
                          />
                        </span>
                      </button>

                      @if (isMatchOpen(row.match.roundMatchId)) {
                        <div class="card-body pt-0 px-3" [id]="'match-' + row.match.roundMatchId">
                          @if (row.entries.length === 0) {
                            <div class="small text-muted">
                              {{ 'publicStandings.noPredictionsHere' | translate }}
                            </div>
                          }
                          @for (e of row.entries; track e.userId) {
                            <div
                              class="match-row d-flex justify-content-between align-items-center small border-bottom py-1 gap-2"
                            >
                              <span
                                class="d-flex align-items-center gap-2"
                                style="min-width: 0"
                                [class.fw-semibold]="e.userId === meId()"
                              >
                                <span
                                  class="rank-avatar"
                                  [style.background]="avatarColor(e.name)"
                                  >{{ initials(e.name) }}</span
                                >
                                <span class="text-truncate">{{ e.name }}</span>
                              </span>
                              <span class="text-end flex-shrink-0">
                                <span class="d-block text-muted text-nowrap"
                                  >{{ 'publicStandings.prediction' | translate }}
                                  <span class="calc text-nowrap"
                                    >{{ e.score.predictedHomeScore ?? '–' }} ×
                                    {{ e.score.predictedAwayScore ?? '–' }}</span
                                  ></span
                                >
                                <span class="d-block text-body">{{
                                  categoryLabel(e.score.scoreCategory) | translate
                                }}</span>
                                <span class="text-muted calc"
                                  >{{ e.score.basePoints }} × {{ e.score.multiplier }} =</span
                                >
                                <span
                                  class="badge ms-1"
                                  [class.text-bg-success]="e.score.finalPoints > 0"
                                  [class.text-bg-secondary]="e.score.finalPoints === 0"
                                  >+{{ e.score.finalPoints }}</span
                                >
                              </span>
                            </div>
                          }
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            }
          }
        }

        <details class="mt-4">
          <summary class="small text-muted">{{ 'publicStandings.howItWorks' | translate }}</summary>
          <div class="small mt-2">
            <div class="row g-1 calc">
              <div class="col-6">
                {{ 'category.ColumnOnly' | translate }}: {{ s.ruleset.columnOnly }}
              </div>
              <div class="col-6">
                {{ 'category.Traditional' | translate }}: {{ s.ruleset.traditional }}
              </div>
              <div class="col-6">{{ 'category.Medium' | translate }}: {{ s.ruleset.medium }}</div>
              <div class="col-6">
                {{ 'category.Uncommon' | translate }}: {{ s.ruleset.uncommon }}
              </div>
              <div class="col-6">
                {{ 'category.ExtraUncommon' | translate }}: {{ s.ruleset.extraUncommon }}
              </div>
              <div class="col-6">{{ 'publicStandings.missed' | translate }}: 0</div>
            </div>
            <div class="text-muted mt-2">{{ 'publicStandings.multiplierNote' | translate }}</div>
          </div>
        </details>

        <footer class="text-center small text-muted mt-4 pt-3 border-top">
          <a class="text-muted" href="/">{{ 'publicStandings.about' | translate }}</a>
        </footer>
      }
    </div>
  `,
})
export class PublicStandings implements OnInit {
  private readonly api = inject(PublicStandingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly meta = inject(Meta);

  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  protected readonly phaseLabel = phaseLabel;

  protected readonly key = signal('');
  protected readonly loading = signal(true);
  protected readonly roundLoading = signal(false);
  protected readonly error = signal(false);
  protected readonly invalidKey = signal(false);
  protected readonly standingsError = signal(false);
  protected readonly roundError = signal(false);
  /** Set when a deep link asked for a round the season does not publish. */
  protected readonly missingRound = signal<number | null>(null);
  protected readonly season = signal<PublicSeason | null>(null);
  protected readonly standings = signal<PublicStandingRow[]>([]);
  protected readonly round = signal<PublicRound | null>(null);
  protected readonly tab = signal<Tab>('overall');
  protected readonly roundNumber = signal<number | null>(null);
  protected readonly expanded = signal<Set<string>>(new Set());

  /** Free-text search over the standings. Filters, never reorders. */
  protected readonly filter = signal('');

  /**
   * Which row the reader claims as their own. There is no session here, so this is a local
   * choice: stored per key, on this device only, and never sent anywhere.
   */
  protected readonly meId = signal<string | null>(null);

  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;
  protected readonly short = shortTeamName;

  /** How the round is sliced: one card per participant, or one per match. */
  protected readonly cut = signal<Cut>('participant');
  protected readonly expandedMatches = signal<Set<string>>(new Set());

  /** Rounds newest-first, as the API returns them. */
  protected readonly rounds = computed(() => this.season()?.rounds ?? []);

  /** Top three, for the podium — same treatment as the in-app standings. */
  protected readonly podium = computed(() => this.standings().slice(0, 3));

  /**
   * The standings with the two gaps a reader actually asks about ("how far off am I?"),
   * then filtered. The deltas are computed before filtering so they keep meaning the
   * moment the list is narrowed down to a single name.
   */
  protected readonly rows = computed(() => {
    const all = this.standings();
    const leader = all[0]?.totalPoints ?? 0;
    const decorated = all.map((row, i) => ({
      ...row,
      toLeader: leader - row.totalPoints,
      toAbove: i === 0 ? 0 : all[i - 1].totalPoints - row.totalPoints,
      above: i === 0 || all[i - 1].totalPoints === leader ? null : all[i - 1].name,
    }));

    const term = this.filter().trim().toLowerCase();
    return term ? decorated.filter((r) => r.name.toLowerCase().includes(term)) : decorated;
  });

  ngOnInit(): void {
    // Keeps the page out of search results. robots.txt stops the crawl; this tag is what
    // de-indexes a URL that was already discovered (e.g. pasted in a public thread). It is
    // removed on leave so the rest of the app is unaffected.
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    this.destroyRef.onDestroy(() => this.meta.removeTag("name='robots'"));

    // The key may arrive in the path (/p/:key) or the query string (/p?key=…).
    const params = this.route.snapshot;
    const key = params.paramMap.get('key') ?? params.queryParamMap.get('key') ?? '';
    this.key.set(key);

    const round = Number(params.queryParamMap.get('rodada'));
    if (Number.isFinite(round) && round > 0) {
      this.roundNumber.set(round);
      this.tab.set('round');
    }
    const participant = params.queryParamMap.get('participante');
    if (participant) {
      this.expanded.set(new Set(participant.split(',').filter(Boolean)));
    }

    this.meId.set(this.readMe());
    this.load();
  }

  load(): void {
    if (!this.key()) {
      this.invalidKey.set(true);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(false);
    this.invalidKey.set(false);

    this.api
      .season(this.key())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (season) => {
          this.season.set(season);
          this.loading.set(false);
          this.loadStandings();
          if (this.tab() === 'round') {
            this.ensureRound();
          }
        },
        error: (err: { status?: number }) => {
          // A bad or unpublished key is the expected case for a stale link, not a fault:
          // it gets its own message instead of the generic retry-me error state.
          if (err?.status === 404) {
            this.invalidKey.set(true);
          } else {
            this.error.set(true);
          }
          this.loading.set(false);
        },
      });
  }

  loadStandings(): void {
    this.standingsError.set(false);
    this.api
      .standings(this.key())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rows) => this.standings.set(rows),
        // Without this, a failed load looks exactly like a season with no points yet.
        error: () => this.standingsError.set(true),
      });
  }

  private ensureRound(): void {
    const available = this.rounds();
    if (available.length === 0) {
      return;
    }
    const wanted = this.roundNumber();
    const found = available.some((r) => r.number === wanted);
    // Falling back silently to another round makes the page look like it ignored the link.
    this.missingRound.set(!found && wanted != null ? wanted : null);
    const number = found ? wanted! : available[0].number;
    this.roundNumber.set(number);

    // Switching tabs back and forth should not re-fetch a round already in hand — it
    // flashes the skeleton and burns the public endpoint's per-IP quota.
    if (this.round()?.number !== number || this.roundError()) {
      this.loadRound(number);
    }
  }

  private loadRound(number: number): void {
    this.roundLoading.set(true);
    this.roundError.set(false);
    this.api
      .round(this.key(), number)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.round.set(r);
          this.roundLoading.set(false);
        },
        error: () => {
          this.round.set(null);
          this.roundError.set(true);
          this.roundLoading.set(false);
        },
      });
  }

  /** Retry hook for the round error state. */
  reloadRound(): void {
    const number = this.roundNumber();
    if (number !== null) {
      this.loadRound(number);
    }
  }

  selectTab(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'round') {
      this.ensureRound();
    }
    this.syncUrl();
  }

  pickRound(value: string): void {
    const number = Number(value);
    if (!Number.isFinite(number)) {
      return;
    }
    this.roundNumber.set(number);
    this.missingRound.set(null);
    this.loadRound(number);
    this.syncUrl();
  }

  /**
   * Jumps from the overall tab into this participant's round breakdown, optionally at the
   * round the reader just pointed at in the history strip.
   */
  auditParticipant(userId: string, roundNumber?: number): void {
    this.expanded.set(new Set([userId]));
    this.cut.set('participant');
    this.tab.set('round');
    if (roundNumber != null) {
      this.roundNumber.set(roundNumber);
    }
    this.ensureRound();
    this.syncUrl();
    // The button that was just pressed no longer exists; without this the focus ring
    // falls back to the document and keyboard readers lose their place.
    setTimeout(() => document.getElementById('round-select')?.focus());
  }

  toggle(userId: string): void {
    const next = new Set(this.expanded());
    if (next.has(userId)) {
      next.delete(userId);
    } else {
      next.add(userId);
    }
    this.expanded.set(next);
    this.syncUrl();
  }

  isOpen(userId: string): boolean {
    return this.expanded().has(userId);
  }

  /**
   * Marks (or unmarks) a row as the reader's own. Pressing it again clears the mark, so a
   * shared phone is never stuck claiming to be someone else.
   */
  markMe(userId: string): void {
    const next = this.meId() === userId ? null : userId;
    this.meId.set(next);
    try {
      if (next) {
        localStorage.setItem(this.meStorageKey(), next);
      } else {
        localStorage.removeItem(this.meStorageKey());
      }
    } catch {
      // Private mode or storage disabled: the highlight simply does not survive the visit.
    }
  }

  private meStorageKey(): string {
    return `palpitao.publicMe.${this.key().toUpperCase()}`;
  }

  private readMe(): string | null {
    try {
      return localStorage.getItem(this.meStorageKey());
    } catch {
      return null;
    }
  }

  score(participant: PublicParticipantScore, matchId: string): PublicMatchScore | null {
    return participant.matchScores.find((s) => s.roundMatchId === matchId) ?? null;
  }

  /**
   * The same round, transposed: every participant's line for one match, best first. Pure
   * client-side work — the round payload already carries both sides of the pivot.
   */
  protected readonly byMatch = computed(() => {
    const round = this.round();
    if (!round) {
      return [];
    }

    return round.matches.map((match) => {
      const entries = round.participants
        .map((p) => ({ userId: p.userId, name: p.name, score: this.score(p, match.roundMatchId) }))
        .filter((e): e is { userId: string; name: string; score: PublicMatchScore } => !!e.score)
        .sort(
          (a, b) =>
            b.score.finalPoints - a.score.finalPoints || a.name.localeCompare(b.name, 'pt-BR'),
        );

      return {
        match,
        entries,
        hits: entries.filter((e) => e.score.finalPoints > 0).length,
      };
    });
  });

  /** A match with no score yet reads as "waiting", never as a missed prediction. */
  hasResult(match: RoundResultMatch): boolean {
    return match.homeScore != null && match.awayScore != null;
  }

  isMatchOpen(matchId: string): boolean {
    return this.expandedMatches().has(matchId);
  }

  toggleMatch(matchId: string): void {
    const next = new Set(this.expandedMatches());
    if (next.has(matchId)) {
      next.delete(matchId);
    } else {
      next.add(matchId);
    }
    this.expandedMatches.set(next);
  }

  /** True when every card of the active cut is already open. */
  allOpen(round: PublicRound): boolean {
    return this.cut() === 'participant'
      ? round.participants.length > 0 && round.participants.every((p) => this.isOpen(p.userId))
      : round.matches.length > 0 && round.matches.every((m) => this.isMatchOpen(m.roundMatchId));
  }

  /**
   * Opens or closes the whole round at once — the one action that makes a full-round
   * screenshot possible, which is what ends up back in the group chat.
   */
  toggleAll(round: PublicRound): void {
    const open = this.allOpen(round);
    if (this.cut() === 'participant') {
      this.expanded.set(open ? new Set() : new Set(round.participants.map((p) => p.userId)));
      this.syncUrl();
    } else {
      this.expandedMatches.set(
        open ? new Set() : new Set(round.matches.map((m) => m.roundMatchId)),
      );
    }
  }

  /**
   * "12–14 mai" for a dated round, empty when the season carries no dates. The month is
   * named once when both ends share it — the locale's own day+month format spells out
   * "12 de mai. - 14 de mai.", which is far too long for a dropdown option.
   */
  roundDates(round: PublicRoundSummary): string {
    if (!round.startDate) {
      return '';
    }

    const locale = this.language.current();
    const month = (d: Date) =>
      new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' }).format(d);
    const day = (d: Date) =>
      new Intl.DateTimeFormat(locale, { day: 'numeric', timeZone: 'UTC' }).format(d);

    const start = new Date(round.startDate);
    if (!round.endDate) {
      return `${day(start)} ${month(start)}`;
    }

    const end = new Date(round.endDate);
    if (day(start) === day(end) && month(start) === month(end)) {
      return `${day(start)} ${month(start)}`;
    }
    return month(start) === month(end)
      ? `${day(start)}–${day(end)} ${month(start)}`
      : `${day(start)} ${month(start)} – ${day(end)} ${month(end)}`;
  }

  categoryLabel(category: ScoreCategory): string {
    return category && category !== ScoreCategory.None
      ? 'category.' + category
      : 'publicStandings.missed';
  }

  setLanguage(lang: Lang): void {
    this.language.use(lang);
  }

  /**
   * Mirrors the current view into the URL so a reader can share the exact slice they are
   * looking at. Replaces history instead of stacking it — expanding three rows should not
   * mean pressing Back three times.
   */
  private syncUrl(): void {
    const open = [...this.expanded()];
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        key: this.route.snapshot.paramMap.get('key') ? null : this.key(),
        rodada: this.tab() === 'round' ? this.roundNumber() : null,
        participante: open.length > 0 ? open.join(',') : null,
      },
      queryParamsHandling: 'merge',
    });
  }
}
