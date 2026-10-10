import { DatePipe } from '@angular/common';
import {
  Component,
  ChangeDetectionStrategy,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  computed,
  signal,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Competition } from '@core/models/enums';
import { FixtureCandidate } from '@core/models';

interface FixtureGroup {
  date: string;
  fixtures: FixtureCandidate[];
}

export interface FixtureSelectionState {
  items: FixtureCandidate[];
  leagueOneJustification: string | null;
  /** False when more than one League One match is selected without a justification. */
  canSave: boolean;
}

/**
 * Presentational panel that renders searched fixtures with multi-select:
 * checkbox per match, select all / clear, a counter, competition + team filters,
 * grouping by date and the League One justification input. Emits the current
 * selection so the parent decides what to do (create a round then import, or
 * import into an existing round).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-fixture-selection',
  imports: [TranslatePipe, DatePipe],
  templateUrl: './fixture-selection.html',
})
export class FixtureSelection implements OnChanges {
  @Input() fixtures: FixtureCandidate[] = [];
  @Input() source = '';
  @Output() readonly selectionChange = new EventEmitter<FixtureSelectionState>();

  private readonly fixtureSignal = signal<FixtureCandidate[]>([]);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly filterCompetition = signal<string>('');
  protected readonly filterTeam = signal<string>('');
  protected readonly leagueOneJustification = signal<string>('');

  ngOnChanges(): void {
    // A new search result resets the selection and filters.
    this.fixtureSignal.set(this.fixtures);
    this.selected.set(new Set());
    this.leagueOneJustification.set('');
    this.filterCompetition.set('');
    this.filterTeam.set('');
    this.emit();
  }

  /**
   * Only the competitions the results actually contain, in the enum's canonical order —
   * the search is scoped to the season's certame, so offering the whole enum would list
   * competitions that can never match (another certame's, or a disabled FA Cup).
   */
  protected readonly competitions = computed(() => {
    const present = new Set(this.fixtureSignal().map((f) => f.competition));
    return Object.values(Competition).filter((c) => present.has(c));
  });

  private readonly filtered = computed(() => {
    const comp = this.filterCompetition();
    const team = this.filterTeam().trim().toLowerCase();
    return this.fixtureSignal().filter((f) => {
      if (comp && f.competition !== comp) return false;
      if (
        team &&
        !f.homeTeamName.toLowerCase().includes(team) &&
        !f.awayTeamName.toLowerCase().includes(team)
      ) {
        return false;
      }
      return true;
    });
  });

  protected readonly groups = computed<FixtureGroup[]>(() => {
    const byDate = new Map<string, FixtureCandidate[]>();
    for (const f of this.filtered()) {
      const date = f.startsAt.slice(0, 10);
      const list = byDate.get(date) ?? [];
      list.push(f);
      byDate.set(date, list);
    }
    return [...byDate.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([date, fx]) => ({ date, fixtures: fx }));
  });

  protected setCompetitionFilter(event: Event): void {
    this.filterCompetition.set((event.target as HTMLSelectElement).value);
  }

  protected setTeamFilter(event: Event): void {
    this.filterTeam.set((event.target as HTMLInputElement).value);
  }

  protected setLeagueOneJustification(event: Event): void {
    this.leagueOneJustification.set((event.target as HTMLInputElement).value);
    this.emit();
  }

  protected toggle(externalId: string): void {
    const next = new Set(this.selected());
    if (next.has(externalId)) next.delete(externalId);
    else next.add(externalId);
    this.selected.set(next);
    this.emit();
  }

  protected selectAll(): void {
    const next = new Set(this.selected());
    for (const f of this.filtered()) {
      if (!f.isAlreadyAddedToRound) next.add(f.externalId);
    }
    this.selected.set(next);
    this.emit();
  }

  protected clearSelection(): void {
    this.selected.set(new Set());
    this.emit();
  }

  private selectedFixtures(): FixtureCandidate[] {
    return this.fixtureSignal().filter((f) => this.selected().has(f.externalId));
  }

  protected leagueOneConflict(): boolean {
    return (
      this.selectedFixtures().filter((f) => f.competition === Competition.LeagueOne).length > 1
    );
  }

  private emit(): void {
    const items = this.selectedFixtures();
    const justification = this.leagueOneJustification().trim();
    this.selectionChange.emit({
      items,
      leagueOneJustification: justification || null,
      canSave: !this.leagueOneConflict() || !!justification,
    });
  }
}
