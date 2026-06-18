import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import {
  Competition,
  MatchPhase,
  RoundStatus,
  TournamentType,
  competitionsForType,
  phasesForType,
} from '../../core/models/enums';
import { FixtureCandidate, Round, RoundMatch, Team } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { MatchesService } from '../../core/services/matches.service';
import { RoundsService } from '../../core/services/rounds.service';
import { TeamsService } from '../../core/services/teams.service';
import {
  FixtureSelection,
  FixtureSelectionState,
} from '../../shared/components/fixture-selection/fixture-selection';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';
import { MatchList } from '../../shared/components/match-list/match-list';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { isoDateFromToday, toImportItem } from '../../shared/utils/fixture.util';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-matches',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    FixtureSelection,
    ErrorState,
    Icon,
    Loading,
    MatchList,
    PageHeader,
  ],
  template: `
    <app-page-header [title]="'adminMatches.title' | translate">
      <div trail class="page-trail">
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        <a [routerLink]="['/admin/rounds', roundId]"
          >{{ 'dashboard.round' | translate }} {{ round()?.number }}</a
        >
        · {{ 'adminMatches.games' | translate }}
      </div>
    </app-page-header>

    @if (loading()) {
      <app-loading />
    } @else if (error()) {
      <app-error-state (retry)="load()" />
    } @else if (round(); as r) {
      @if (!editable()) {
        <div class="alert alert-warning py-2">{{ 'adminMatches.notEditable' | translate }}</div>
      } @else {
        <!-- Add / edit a match -->
        <div class="card mb-3">
          <div class="card-body p-4">
            <div class="d-flex align-items-center gap-2 mb-3">
              <span class="icon-tile icon-tile--blue"><app-icon name="plus" [size]="20" /></span>
              <h2 class="h6 fw-bold mb-0">
                {{ (editingId() ? 'adminMatches.editGame' : 'adminMatches.addGame') | translate }}
              </h2>
            </div>
            <form [formGroup]="form" (ngSubmit)="save()" class="vstack gap-3">
              <div>
                <label class="form-label">{{ 'adminMatches.competition' | translate }}</label>
                <div class="input-group input-group-lg">
                  <span class="input-group-text"><app-icon name="trophy" [size]="16" /></span>
                  <select class="form-select" formControlName="competition">
                    @for (c of competitions(); track c.value) {
                      <option [value]="c.value">{{ c.label }}</option>
                    }
                  </select>
                </div>
              </div>

              <div>
                <label class="form-label">{{ 'adminMatches.phase' | translate }}</label>
                <div class="input-group input-group-lg">
                  <span class="input-group-text"><app-icon name="shuffle" [size]="16" /></span>
                  <select class="form-select" formControlName="phase">
                    @for (p of phases(); track p) {
                      <option [value]="p">{{ 'phase.' + p | translate }}</option>
                    }
                  </select>
                </div>
              </div>

              <div class="d-flex align-items-center gap-2">
                <div class="input-group input-group-lg flex-grow-1">
                  <span class="input-group-text"><app-icon name="house" [size]="16" /></span>
                  <select class="form-select" formControlName="homeTeamId">
                    <option value="">{{ 'adminMatches.home' | translate }}</option>
                    @for (t of filteredTeams(); track t.id) {
                      <option [value]="t.id">{{ t.name }}</option>
                    }
                  </select>
                </div>
                <span class="vs-badge">×</span>
                <div class="input-group input-group-lg flex-grow-1">
                  <span class="input-group-text"><app-icon name="plane" [size]="16" /></span>
                  <select class="form-select" formControlName="awayTeamId">
                    <option value="">{{ 'adminMatches.away' | translate }}</option>
                    @for (t of filteredTeams(); track t.id) {
                      <option [value]="t.id">{{ t.name }}</option>
                    }
                  </select>
                </div>
              </div>

              <div class="row g-3">
                <div class="col-md-6">
                  <label class="form-label">{{ 'adminMatches.datetime' | translate }}</label>
                  <div class="input-group input-group-lg">
                    <span class="input-group-text"
                      ><app-icon name="calendar-days" [size]="16"
                    /></span>
                    <input type="datetime-local" class="form-control" formControlName="startsAt" />
                  </div>
                </div>
                <div class="col-md-6">
                  <label class="form-label">{{
                    'adminMatches.manualMultiplier' | translate
                  }}</label>
                  <div class="input-group input-group-lg">
                    <span class="input-group-text"><app-icon name="x" [size]="16" /></span>
                    <input
                      type="number"
                      min="1"
                      class="form-control"
                      formControlName="manualMultiplierOverride"
                      [placeholder]="'adminMatches.multiplierPlaceholder' | translate"
                    />
                  </div>
                </div>
              </div>

              @if (form.controls.manualMultiplierOverride.value) {
                <input
                  class="form-control"
                  [class.is-invalid]="justificationMissing()"
                  [placeholder]="'adminMatches.justification' | translate"
                  formControlName="manualMultiplierJustification"
                />
                @if (justificationMissing()) {
                  <div class="invalid-feedback d-block">
                    {{ 'adminMatches.needJustification' | translate }}
                  </div>
                }
              }

              @if (leagueOneWarning()) {
                <div class="alert alert-warning py-2 mb-0">
                  {{ 'adminMatches.leagueOneWarning' | translate }}
                </div>
              }

              <div class="d-flex gap-2">
                <button
                  type="submit"
                  class="btn btn-primary btn-lg flex-fill"
                  [disabled]="form.invalid || saving() || justificationMissing()"
                >
                  <app-icon name="plus" [size]="16" />
                  {{ (editingId() ? 'adminMatches.save' : 'adminMatches.addGame') | translate }}
                </button>
                @if (editingId()) {
                  <button
                    type="button"
                    class="btn btn-outline-secondary btn-lg"
                    (click)="resetForm()"
                  >
                    {{ 'common.cancel' | translate }}
                  </button>
                }
              </div>
            </form>
          </div>
        </div>

        <!-- Import fixtures from the external source into this existing round -->
        <div class="card mb-3">
          <div class="card-body p-4">
            <div class="d-flex align-items-center gap-2 mb-3">
              <span class="icon-tile icon-tile--amber"
                ><app-icon name="calendar-days" [size]="20"
              /></span>
              <h2 class="h6 fw-bold mb-0">{{ 'fixtures.importTitle' | translate }}</h2>
            </div>
            <form [formGroup]="searchForm" class="vstack gap-3">
              <div class="row g-3">
                <div class="col-6">
                  <label class="form-label">{{ 'roundForm.startDate' | translate }}</label>
                  <div class="input-group input-group-lg">
                    <span class="input-group-text"
                      ><app-icon name="calendar-days" [size]="16"
                    /></span>
                    <input type="date" class="form-control" formControlName="startDate" />
                  </div>
                </div>
                <div class="col-6">
                  <label class="form-label">{{ 'roundForm.endDate' | translate }}</label>
                  <div class="input-group input-group-lg">
                    <span class="input-group-text"
                      ><app-icon name="calendar-days" [size]="16"
                    /></span>
                    <input type="date" class="form-control" formControlName="endDate" />
                  </div>
                </div>
              </div>
              @if (searchRangeInvalid()) {
                <div class="text-danger small">{{ 'fixtures.endBeforeStart' | translate }}</div>
              }
              <button
                type="button"
                class="btn btn-soft-primary btn-lg w-100"
                [disabled]="!canSearch() || searching()"
                (click)="searchFixtures()"
              >
                @if (searching()) {
                  <span class="spinner-border spinner-border-sm me-2"></span>
                }
                <app-icon name="search" [size]="16" /> {{ 'fixtures.searchButton' | translate }}
              </button>
            </form>

            @if (searched()) {
              <hr />
              <app-fixture-selection
                [fixtures]="fixtures()"
                [source]="source()"
                (selectionChange)="onSelection($event)"
              />
              <button
                type="button"
                class="btn btn-primary w-100 mt-2"
                [disabled]="selection().items.length === 0 || !selection().canSave || importing()"
                (click)="importSelected()"
              >
                {{ 'fixtures.addSelected' | translate: { count: selection().items.length } }}
              </button>
            }

            @if (searchError()) {
              <div class="alert alert-warning py-2 mt-2 mb-0">
                {{ 'fixtures.searchError' | translate }}
              </div>
            }
          </div>
        </div>
      }

      <h2 class="h6 fw-bold mb-2">
        {{ 'adminMatches.games' | translate }}
        <span class="badge text-bg-light ms-1">{{ r.matches.length }}</span>
      </h2>
      <app-match-list
        [matches]="r.matches"
        [editable]="editable()"
        (edit)="edit($event)"
        (remove)="remove($event)"
      />
    }
  `,
  styles: [
    `
      .vs-badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2.5rem;
        height: 2.5rem;
        border-radius: 50%;
        background: #161d2c;
        color: #fff;
        font-weight: 700;
        flex: none;
      }
      .flex-none {
        flex: none;
      }
    `,
  ],
})
export class AdminMatches implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly matchesApi = inject(MatchesService);
  private readonly teamsApi = inject(TeamsService);
  private readonly adminApi = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  private static readonly COMPETITION_LABELS: Record<Competition, string> = {
    [Competition.PremierLeague]: 'Premier League',
    [Competition.FACup]: 'FA Cup',
    [Competition.Championship]: 'Championship',
    [Competition.LeagueOne]: 'League One',
    [Competition.FifaWorldCup]: 'FIFA World Cup',
  };

  /** Competitions/phases offered depend on the round's season certame type. */
  protected readonly competitions = computed(() =>
    competitionsForType(this.round()?.tournamentType).map((value) => ({
      value,
      label: AdminMatches.COMPETITION_LABELS[value],
    })),
  );
  protected readonly phases = computed(() => phasesForType(this.round()?.tournamentType));
  private get isWorldCup(): boolean {
    return this.round()?.tournamentType === TournamentType.FifaWorldCup;
  }

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly saving = signal(false);
  protected readonly round = signal<Round | null>(null);
  protected readonly teams = signal<Team[]>([]);
  protected readonly editingId = signal<string | null>(null);
  protected roundId = '';

  protected readonly editable = computed(() => {
    const s = this.round()?.status;
    return s === RoundStatus.Draft || s === RoundStatus.Published;
  });

  protected readonly leagueOneWarning = computed(() => {
    if (this.form.controls.competition.value !== Competition.LeagueOne) return false;
    const others = (this.round()?.matches ?? []).filter(
      (m) => m.competition === Competition.LeagueOne && m.id !== this.editingId(),
    );
    return others.length >= 1;
  });

  protected readonly form = this.fb.nonNullable.group({
    // Defaults are England; once the round (and its season type) loads they are
    // re-applied via resetForm() so a World Cup round starts on valid options.
    competition: [Competition.PremierLeague, Validators.required],
    phase: [MatchPhase.Regular, Validators.required],
    homeTeamId: ['', Validators.required],
    awayTeamId: ['', Validators.required],
    startsAt: ['', Validators.required],
    manualMultiplierOverride: [null as number | null],
    manualMultiplierJustification: [''],
  });

  /** Tracks the selected competition so the team dropdowns can react to it. */
  private readonly selectedCompetition = toSignal(this.form.controls.competition.valueChanges, {
    initialValue: this.form.controls.competition.value,
  });

  /**
   * Clubs offered in the home/away dropdowns. For a tracked league division only
   * its clubs are shown; the FA Cup (drawing from every division) shows them all.
   */
  protected readonly filteredTeams = computed(() => {
    const comp = this.selectedCompetition();
    const all = this.teams();
    if (
      comp === Competition.PremierLeague ||
      comp === Competition.Championship ||
      comp === Competition.LeagueOne
    ) {
      return all.filter((t) => t.division === comp);
    }
    return all;
  });

  constructor() {
    // When the competition changes, drop any selected club that no longer
    // belongs to the filtered list so a wrong-division team can't be submitted.
    effect(() => {
      const valid = new Set(this.filteredTeams().map((t) => t.id));
      const home = this.form.controls.homeTeamId;
      const away = this.form.controls.awayTeamId;
      if (home.value && !valid.has(home.value)) home.setValue('');
      if (away.value && !valid.has(away.value)) away.setValue('');
    });
  }

  // --- External fixture import (into this existing round) -----------------
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  /** True when the last fixture search failed (source down) — show the manual fallback hint. */
  protected readonly searchError = signal(false);
  protected readonly importing = signal(false);
  protected readonly source = signal('');
  protected readonly fixtures = signal<FixtureCandidate[]>([]);
  protected readonly selection = signal<FixtureSelectionState>({
    items: [],
    leagueOneJustification: null,
    canSave: true,
  });

  protected readonly searchForm = this.fb.nonNullable.group({
    startDate: [''],
    endDate: [''],
  });

  ngOnInit(): void {
    this.roundId = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    forkJoin({
      round: this.roundsApi.getById(this.roundId),
      teams: this.teamsApi.list(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ round, teams }) => {
          this.round.set(round);
          this.teams.set(teams);
          // Apply season-type-aware defaults to the (empty) add form.
          this.resetForm();
          // Default the fixture-search window to the round's own period when set,
          // otherwise to the next 8 days, and run a pre-search so the list is ready.
          const start = round.startDate?.slice(0, 10) ?? isoDateFromToday(0);
          const end = round.endDate?.slice(0, 10) ?? isoDateFromToday(8);
          this.searchForm.patchValue({ startDate: start, endDate: end });
          this.loading.set(false);
          if (this.editableStatus(round)) {
            this.preSearch();
          }
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  private editableStatus(round: Round): boolean {
    return round.status === RoundStatus.Draft || round.status === RoundStatus.Published;
  }

  /**
   * Automatic pre-search on load: populates the fixture list silently (no error
   * toast if the source is down) and only reveals it when there are results.
   */
  private preSearch(): void {
    if (!this.canSearch()) return;
    const { startDate, endDate } = this.searchForm.getRawValue();
    this.searching.set(true);
    this.searchError.set(false);
    this.adminApi
      .searchFixtures(
        {
          startDate: `${startDate}T00:00:00`,
          endDate: `${endDate}T23:59:59`,
          roundId: this.roundId,
        },
        { silent: true },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if ((res.fixtures?.length ?? 0) > 0) {
            this.fixtures.set(res.fixtures);
            this.source.set(res.source);
            this.searched.set(true);
          }
          this.searching.set(false);
        },
        error: () => {
          this.searchError.set(true);
          this.searching.set(false);
        },
      });
  }

  reload(): void {
    this.roundsApi
      .getById(this.roundId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((r) => this.round.set(r));
  }

  // --- External fixture import -------------------------------------------
  protected searchRangeInvalid(): boolean {
    const { startDate, endDate } = this.searchForm.getRawValue();
    return !!startDate && !!endDate && endDate < startDate;
  }

  protected canSearch(): boolean {
    const { startDate, endDate } = this.searchForm.getRawValue();
    return !!startDate && !!endDate && !this.searchRangeInvalid();
  }

  protected searchFixtures(): void {
    if (!this.canSearch()) return;
    const { startDate, endDate } = this.searchForm.getRawValue();
    this.searching.set(true);
    this.searchError.set(false);
    this.adminApi
      .searchFixtures({
        startDate: `${startDate}T00:00:00`,
        endDate: `${endDate}T23:59:59`,
        roundId: this.roundId,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.fixtures.set(res.fixtures);
          this.source.set(res.source);
          this.searched.set(true);
          this.searching.set(false);
        },
        error: () => {
          this.searchError.set(true);
          this.searching.set(false);
        },
      });
  }

  protected onSelection(state: FixtureSelectionState): void {
    this.selection.set(state);
  }

  protected importSelected(): void {
    const state = this.selection();
    if (state.items.length === 0 || !state.canSave) return;
    this.importing.set(true);
    this.adminApi
      .importFixtures(this.roundId, {
        fixtures: state.items.map(toImportItem),
        leagueOneJustification: state.leagueOneJustification,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.toast.success(
            this.translate.instant('fixtures.importedToast', {
              imported: res.importedCount,
              skipped: res.skippedDuplicateCount,
            }),
          );
          this.importing.set(false);
          this.searched.set(false);
          this.fixtures.set([]);
          this.reload();
        },
        error: () => this.importing.set(false),
      });
  }

  edit(m: RoundMatch): void {
    this.editingId.set(m.id);
    this.form.setValue({
      competition: m.competition,
      phase: m.phase,
      homeTeamId: m.homeTeamId,
      awayTeamId: m.awayTeamId,
      startsAt: m.startsAt.substring(0, 16),
      manualMultiplierOverride: m.manualMultiplierOverride ?? null,
      manualMultiplierJustification: m.manualMultiplierJustification ?? '',
    });
  }

  resetForm(): void {
    this.editingId.set(null);
    this.form.reset({
      competition: this.isWorldCup ? Competition.FifaWorldCup : Competition.PremierLeague,
      phase: this.isWorldCup ? MatchPhase.WorldCupGroupStage : MatchPhase.Regular,
      homeTeamId: '',
      awayTeamId: '',
      startsAt: '',
      manualMultiplierOverride: null,
      manualMultiplierJustification: '',
    });
  }

  /**
   * True when a manual multiplier override is set but the (required) justification
   * is still empty. Drives the invalid-feedback and disables Save, so the rule is
   * visible up front instead of only as a toast at submit time.
   */
  justificationMissing(): boolean {
    const { manualMultiplierOverride, manualMultiplierJustification } = this.form.controls;
    return (
      manualMultiplierOverride.value != null && !(manualMultiplierJustification.value ?? '').trim()
    );
  }

  save(): void {
    const v = this.form.getRawValue();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (v.homeTeamId === v.awayTeamId) {
      this.toast.error(this.translate.instant('adminMatches.sameTeam'));
      return;
    }
    if (v.manualMultiplierOverride != null && !v.manualMultiplierJustification.trim()) {
      this.toast.error(this.translate.instant('adminMatches.needJustification'));
      return;
    }

    const body = {
      competition: v.competition,
      phase: v.phase,
      homeTeamId: v.homeTeamId,
      awayTeamId: v.awayTeamId,
      startsAt: v.startsAt,
      manualMultiplierOverride: v.manualMultiplierOverride,
      manualMultiplierJustification: v.manualMultiplierJustification || null,
    };

    this.saving.set(true);
    const id = this.editingId();
    const request$ = id
      ? this.matchesApi.update(id, body)
      : this.roundsApi.addMatch(this.roundId, body);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('adminMatches.saved'));
        this.saving.set(false);
        this.resetForm();
        this.reload();
      },
      error: () => this.saving.set(false),
    });
  }

  async remove(m: RoundMatch): Promise<void> {
    const ok = await this.confirm.ask(
      this.translate.instant('adminMatches.confirmRemove', {
        home: m.homeTeamName,
        away: m.awayTeamName,
      }),
      {
        title: this.translate.instant('common.remove'),
        confirmText: this.translate.instant('common.remove'),
        danger: true,
      },
    );
    if (ok) {
      this.matchesApi
        .remove(m.id)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.toast.success(this.translate.instant('adminMatches.removed'));
            this.reload();
          },
        });
    }
  }
}
