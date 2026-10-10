import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FixtureCandidate } from '@core/models';
import { AdminFixturesService } from '@core/services/admin/admin-fixtures.service';
import { FixtureSelectionState } from '@shared/components/fixture-selection/fixture-selection';

/**
 * The external-fixture search shared by the round form (a round being created, scoped by its
 * season) and the round's match editor (scoped by the round): the search state, the results and
 * the admin's selection. Provided per component, so each page gets its own and it is torn down
 * with it.
 */
@Injectable()
export class FixtureSearchStore {
  private readonly fixturesApi = inject(AdminFixturesService);
  private readonly destroyRef = inject(DestroyRef);

  readonly searching = signal(false);
  readonly searched = signal(false);
  /** True when the last search failed (source down) — show the manual fallback hint. */
  readonly searchError = signal(false);
  readonly source = signal('');
  readonly fixtures = signal<FixtureCandidate[]>([]);
  readonly selection = signal<FixtureSelectionState>({
    items: [],
    leagueOneJustification: null,
    canSave: true,
  });

  /**
   * Searches the fixtures between two dates (`yyyy-MM-dd`, both days included). A silent search
   * — the automatic one on load — pops no error toast and only shows its results when there are
   * any.
   */
  search(
    range: { startDate: string; endDate: string },
    scope: { roundId?: string; seasonId?: string },
    options: { silent?: boolean } = {},
  ): void {
    this.searching.set(true);
    this.searchError.set(false);
    this.fixturesApi
      .searchFixtures(
        {
          startDate: `${range.startDate}T00:00:00`,
          endDate: `${range.endDate}T23:59:59`,
          ...scope,
        },
        { silent: options.silent },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (!options.silent || (res.fixtures?.length ?? 0) > 0) {
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
}

/** True when both dates are set and the range runs backwards. */
export function isRangeInvalid(startDate: string, endDate: string): boolean {
  return !!startDate && !!endDate && endDate < startDate;
}
