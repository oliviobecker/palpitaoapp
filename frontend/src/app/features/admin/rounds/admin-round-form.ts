import { AdminFixturesService } from '@core/services/admin/admin-fixtures.service';
import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { HasUnsavedChanges } from '@core/guards/unsaved-changes.guard';
import { LanguageService } from '@core/i18n/language.service';
import { FixtureCandidate, RoundSummary, Season } from '@core/models';
import { ToastService } from '@core/notifications/toast.service';
import { RoundsService } from '@core/services/rounds.service';
import { SeasonsService } from '@core/services/seasons.service';
import {
  FixtureSelection,
  FixtureSelectionState,
} from '@shared/components/fixture-selection/fixture-selection';
import { FormField } from '@shared/components/form-field/form-field';
import { Icon } from '@shared/components/icon/icon';
import { PageHeader } from '@shared/components/page-header/page-header';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { isoDateFromToday, toImportItem } from '@shared/utils/fixture.util';
import { joinPreview, ordinalRoundName } from '@shared/utils/round-name.util';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-round-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    FixtureSelection,
    FormField,
    Icon,
    PageHeader,
    RoundLabelPipe,
  ],
  templateUrl: './admin-round-form.html',
})
export class AdminRoundForm implements OnInit, HasUnsavedChanges {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly roundsApi = inject(RoundsService);
  private readonly fixturesApi = inject(AdminFixturesService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly seasons = signal<Season[]>([]);
  private readonly rounds = signal<RoundSummary[]>([]);
  /** While true, the title auto-follows the number ("Primeira Rodada", …). */
  private autoTitle = true;
  protected readonly saving = signal(false);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  /** True when the last fixture search failed (source down) — show the manual fallback hint. */
  protected readonly searchError = signal(false);
  protected readonly source = signal('');
  protected readonly fixtures = signal<FixtureCandidate[]>([]);
  protected readonly selection = signal<FixtureSelectionState>({
    items: [],
    leagueOneJustification: null,
    canSave: true,
  });

  protected readonly form = this.fb.nonNullable.group({
    seasonId: ['', Validators.required],
    number: [1, [Validators.required, Validators.min(1)]],
    /** Play the new round as the next part of the previous round ("10.2"). */
    joinPreviousWeek: [false],
    title: [''],
    // Default the round window to today → +10 days (admin can adjust).
    startDate: [isoDateFromToday(0), Validators.required],
    endDate: [isoDateFromToday(10), Validators.required],
  });

  constructor() {
    // Keep the name in sync with the number while it's still auto-generated.
    this.form.controls.number.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.refreshAutoTitle());
    // A part takes the name of the round it joins ("Décima Rodada" for 10.2).
    this.form.controls.joinPreviousWeek.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.refreshAutoTitle());
    // A manual edit of the name stops the auto-sync.
    this.form.controls.title.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.autoTitle = false;
    });
    // Re-number sequentially whenever the season changes, and refresh the suggestions:
    // the search is scoped to the season's certame, so they differ per season.
    this.form.controls.seasonId.valueChanges.pipe(takeUntilDestroyed()).subscribe((seasonId) => {
      this.applyNextNumber(seasonId);
      this.preSearch();
    });
  }

  ngOnInit(): void {
    forkJoin({ seasons: this.seasonsApi.list(), rounds: this.roundsApi.getAll() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ seasons, rounds }) => {
        this.seasons.set(seasons);
        this.rounds.set(Array.isArray(rounds) ? rounds : []);
        const active = seasons.find((s) => s.isActive);
        if (active) {
          // Cascades: seasonId → next number → auto name → pre-search.
          this.form.patchValue({ seasonId: active.id });
        } else {
          this.applyNextNumber('');
          // No season to scope by, but the default window (today → +10) is still worth
          // pre-searching so suggestions are ready.
          this.preSearch();
        }
      });
  }

  /** Used by the unsaved-changes route guard (fixtures picked or fields edited). */
  hasUnsavedChanges(): boolean {
    if (this.saving()) return false;
    return this.form.dirty || this.selection().items.length > 0;
  }

  /** Next sequential number for a season = highest existing + 1 (or 1). */
  private applyNextNumber(seasonId: string): void {
    const numbers = this.rounds()
      .filter((r) => r.seasonId === seasonId)
      .map((r) => r.number);
    const next = numbers.length ? Math.max(...numbers) + 1 : 1;
    this.form.controls.number.setValue(next, { emitEvent: false });
    this.refreshAutoTitle();
  }

  /**
   * Where the new round lands when played in the previous round's week — the same rule the
   * server applies — or null when the season has no previous round to join.
   */
  protected joinTarget(): { number: number; part: number; scored: boolean } | null {
    const { seasonId, number } = this.form.getRawValue();
    return joinPreview(
      this.rounds().filter((r) => r.seasonId === seasonId),
      number,
    );
  }

  /** The default name follows the number — or, for a part, the round it joins. */
  private refreshAutoTitle(): void {
    if (!this.autoTitle) return;
    const { number, joinPreviousWeek } = this.form.getRawValue();
    const n = (joinPreviousWeek ? this.joinTarget()?.number : null) ?? number;
    if (n) {
      this.form.controls.title.setValue(ordinalRoundName(n, this.language.current()), {
        emitEvent: false,
      });
    }
  }

  /**
   * Automatic pre-search on load: populates the fixture list silently (no error
   * toast if the source is down) and only reveals it when there are results.
   */
  private preSearch(): void {
    if (!this.canSearch()) return;
    const { startDate, endDate, seasonId } = this.form.getRawValue();
    this.searching.set(true);
    this.searchError.set(false);
    this.fixturesApi
      .searchFixtures(
        {
          startDate: `${startDate}T00:00:00`,
          endDate: `${endDate}T23:59:59`,
          seasonId: seasonId || undefined,
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

  // --- Date helpers -------------------------------------------------------
  protected dateRangeInvalid(): boolean {
    const { startDate, endDate } = this.form.getRawValue();
    return !!startDate && !!endDate && endDate < startDate;
  }

  protected canSearch(): boolean {
    const { startDate, endDate } = this.form.getRawValue();
    return !!startDate && !!endDate && !this.dateRangeInvalid();
  }

  // --- Search -------------------------------------------------------------
  protected search(): void {
    if (!this.canSearch()) return;
    const { startDate, endDate, seasonId } = this.form.getRawValue();
    this.searching.set(true);
    this.searchError.set(false);
    this.fixturesApi
      .searchFixtures({
        startDate: `${startDate}T00:00:00`,
        endDate: `${endDate}T23:59:59`,
        seasonId: seasonId || undefined,
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

  // --- Save ---------------------------------------------------------------
  protected save(): void {
    const state = this.selection();
    if (this.form.invalid || this.dateRangeInvalid() || !state.canSave) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { seasonId, number, joinPreviousWeek, title, startDate, endDate } =
      this.form.getRawValue();
    this.roundsApi
      .create({
        seasonId,
        number,
        title: title || null,
        startDate: `${startDate}T00:00:00`,
        endDate: `${endDate}T23:59:59`,
        // Hidden once there is nothing to join (another season picked): never sent stale.
        joinPreviousWeek: joinPreviousWeek && this.joinTarget() !== null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (round) => {
          if (state.items.length === 0) {
            // No fixtures picked/found — land on the manual add/edit screen.
            this.toast.success(this.translate.instant('roundForm.created'));
            this.router.navigate(['/admin/rounds', round.id, 'matches']);
            return;
          }
          this.fixturesApi
            .importFixtures(round.id, {
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
                this.router.navigate(['/admin/rounds', round.id]);
              },
              error: () => {
                // Round was created; let the admin finish on the detail screen.
                this.saving.set(false);
                this.router.navigate(['/admin/rounds', round.id]);
              },
            });
        },
        error: () => this.saving.set(false),
      });
  }
}
