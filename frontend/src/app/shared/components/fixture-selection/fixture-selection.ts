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
import { Competition } from '../../../core/models/enums';
import { FixtureCandidate } from '../../../core/models/models';

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
  template: `
    @if (fixtures.length === 0) {
      <p class="text-muted mb-0">{{ 'fixtures.noneFound' | translate }}</p>
    } @else {
      <div class="d-flex justify-content-between align-items-center mb-2">
        <span class="fw-semibold">{{
          'fixtures.selectedCount' | translate: { count: selected().size }
        }}</span>
        @if (source) {
          <span class="badge text-bg-light">{{ source }}</span>
        }
      </div>

      <div class="d-flex gap-2 mb-2">
        <button type="button" class="btn btn-sm btn-outline-secondary" (click)="selectAll()">
          {{ 'fixtures.selectAll' | translate }}
        </button>
        <button type="button" class="btn btn-sm btn-outline-secondary" (click)="clearSelection()">
          {{ 'fixtures.clear' | translate }}
        </button>
      </div>

      <div class="row g-2 mb-3">
        <div class="col-6">
          <select
            class="form-select form-select-sm"
            [attr.aria-label]="'fixtures.filterByCompetition' | translate"
            [value]="filterCompetition()"
            (change)="setCompetitionFilter($event)"
          >
            <option value="">{{ 'fixtures.allCompetitions' | translate }}</option>
            @for (c of competitions; track c) {
              <option [value]="c">{{ 'fixtures.comp.' + c | translate }}</option>
            }
          </select>
        </div>
        <div class="col-6">
          <input
            class="form-control form-control-sm"
            [placeholder]="'fixtures.searchTeam' | translate"
            [attr.aria-label]="'fixtures.searchTeam' | translate"
            [value]="filterTeam()"
            (input)="setTeamFilter($event)"
          />
        </div>
      </div>

      @for (group of groups(); track group.date) {
        <div class="text-uppercase text-muted small fw-bold mt-2 mb-1">{{ group.date }}</div>
        @for (f of group.fixtures; track f.externalId) {
          <div
            class="card mb-2"
            [class.border-primary]="selected().has(f.externalId)"
            [class.opacity-50]="f.isAlreadyAddedToRound"
          >
            <div class="card-body py-2">
              <div class="d-flex align-items-start gap-2">
                <input
                  type="checkbox"
                  class="form-check-input mt-1"
                  style="transform: scale(1.4)"
                  [checked]="selected().has(f.externalId)"
                  [disabled]="f.isAlreadyAddedToRound"
                  (change)="toggle(f.externalId)"
                />
                <div class="flex-grow-1">
                  <div class="d-flex flex-wrap gap-1 mb-1">
                    <span class="badge text-bg-secondary">{{
                      'fixtures.comp.' + f.competition | translate
                    }}</span>
                    @if (f.suggestedMultiplier > 1) {
                      <span class="badge text-bg-warning">x{{ f.suggestedMultiplier }}</span>
                    }
                    @if (f.isBigSevenMatch) {
                      <span class="badge text-bg-danger">{{ 'fixtures.classic' | translate }}</span>
                    }
                    @if (f.isAlreadyAddedToRound) {
                      <span class="badge text-bg-light">{{
                        'fixtures.alreadyAdded' | translate
                      }}</span>
                    }
                  </div>
                  <div class="small text-muted">{{ f.startsAt | date: 'dd/MM/yyyy HH:mm' }}</div>
                  <div class="fw-semibold">{{ f.homeTeamName }}</div>
                  <div class="text-muted small">vs</div>
                  <div class="fw-semibold">{{ f.awayTeamName }}</div>
                  <div class="text-muted small mt-1">
                    {{ 'fixtures.source' | translate }}: {{ f.source }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }
      }

      @if (leagueOneConflict()) {
        <label for="lo-justification" class="form-label small mb-0 mt-2 text-warning">{{
          'fixtures.leagueOneSingle' | translate
        }}</label>
        <input
          id="lo-justification"
          class="form-control"
          [value]="leagueOneJustification()"
          (input)="setLeagueOneJustification($event)"
          [placeholder]="'fixtures.justification' | translate"
        />
      }
    }
  `,
})
export class FixtureSelection implements OnChanges {
  @Input() fixtures: FixtureCandidate[] = [];
  @Input() source = '';
  @Output() readonly selectionChange = new EventEmitter<FixtureSelectionState>();

  protected readonly competitions = Object.values(Competition);

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
