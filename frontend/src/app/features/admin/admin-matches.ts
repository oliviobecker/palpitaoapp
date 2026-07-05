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
import { FormField } from '../../shared/components/form-field/form-field';
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
    FormField,
    Icon,
    Loading,
    MatchList,
    PageHeader,
  ],
  templateUrl: './admin-matches.html',
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
