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
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ScoreCategory } from '../../core/models/enums';
import {
  ScoringConfig,
  ScoringConfigRequest,
  ScoringConfigTeam,
  ScoringMultiplierRule,
  Season,
} from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { ScoringConfigService } from '../../core/services/scoring-config.service';
import { SeasonsService } from '../../core/services/seasons.service';
import { StandingsService } from '../../core/services/standings.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { phaseLabel } from '../../shared/utils/match.util';

const MAX_GOALS = 6;

/** Categories an admin can assign to an exact score (ExtraUncommon is the catch-all). */
const ASSIGNABLE: ScoreCategory[] = [
  ScoreCategory.Traditional,
  ScoreCategory.Medium,
  ScoreCategory.Uncommon,
  ScoreCategory.ExtraUncommon,
];

interface BasePointsForm {
  columnOnly: number;
  traditional: number;
  medium: number;
  uncommon: number;
  extraUncommon: number;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-scoring-rules',
  imports: [TranslatePipe, CompetitionBadge, ErrorState, Icon, Loading, PageHeader],
  templateUrl: './admin-scoring-rules.html',
})
export class AdminScoringRules implements OnInit {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly configApi = inject(ScoringConfigService);
  private readonly standingsApi = inject(StandingsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly assignable = ASSIGNABLE;
  protected readonly phaseLabel = phaseLabel;

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly saving = signal(false);
  protected readonly recalculating = signal(false);

  protected readonly seasons = signal<Season[]>([]);
  protected readonly seasonId = signal('');
  protected readonly config = signal<ScoringConfig | null>(null);

  // Editable state (copies of the loaded config).
  protected readonly basePoints = signal<BasePointsForm>({
    columnOnly: 0,
    traditional: 0,
    medium: 0,
    uncommon: 0,
    extraUncommon: 0,
  });
  /** Map of "low-high" → category for the score grid (ExtraUncommon = unmapped default). */
  protected readonly scoreCategories = signal<Record<string, ScoreCategory>>({});
  protected readonly multiplierRules = signal<ScoringMultiplierRule[]>([]);
  protected readonly teams = signal<ScoringConfigTeam[]>([]);
  protected readonly teamFilter = signal('');

  protected readonly hasScoredRounds = computed(() => this.config()?.hasScoredRounds ?? false);
  protected readonly selectedTeamCount = computed(
    () => this.teams().filter((t) => t.isClassic).length,
  );
  protected readonly filteredTeams = computed(() => {
    const q = this.teamFilter().trim().toLowerCase();
    const list = this.teams();
    return q ? list.filter((t) => t.name.toLowerCase().includes(q)) : list;
  });

  /** Candidate normalized scores (low ≤ high) for the category grid. */
  protected readonly scoreGrid = computed(() => {
    const cells: { low: number; high: number; key: string }[] = [];
    for (let low = 0; low <= MAX_GOALS; low++) {
      for (let high = low; high <= MAX_GOALS; high++) {
        cells.push({ low, high, key: `${low}-${high}` });
      }
    }
    return cells;
  });

  ngOnInit(): void {
    this.loadSeasons();
  }

  loadSeasons(): void {
    this.loading.set(true);
    this.error.set(false);
    this.seasonsApi
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (seasons) => {
          this.seasons.set(seasons);
          const active = seasons.find((s) => s.isActive) ?? seasons[0];
          if (active) {
            this.seasonId.set(active.id);
            this.loadConfig(active.id);
          } else {
            this.loading.set(false);
          }
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  onSeasonChange(id: string): void {
    this.seasonId.set(id);
    this.loadConfig(id);
  }

  loadConfig(seasonId: string): void {
    this.loading.set(true);
    this.error.set(false);
    this.configApi
      .get(seasonId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (config) => {
          this.applyConfig(config);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  private applyConfig(config: ScoringConfig): void {
    this.config.set(config);
    this.basePoints.set({ ...config.basePoints });
    const map: Record<string, ScoreCategory> = {};
    for (const e of config.scoreEntries) {
      map[`${e.low}-${e.high}`] = e.category;
    }
    this.scoreCategories.set(map);
    this.multiplierRules.set(config.multiplierRules.map((r) => ({ ...r })));
    this.teams.set(config.teams.map((t) => ({ ...t })));
    this.teamFilter.set('');
  }

  // --- Base points -------------------------------------------------------
  setBasePoint(field: keyof BasePointsForm, value: string): void {
    this.basePoints.update((bp) => ({ ...bp, [field]: this.toInt(value) }));
  }

  // --- Score categories --------------------------------------------------
  category(key: string): ScoreCategory {
    return this.scoreCategories()[key] ?? ScoreCategory.ExtraUncommon;
  }

  setCategory(key: string, value: string): void {
    this.scoreCategories.update((m) => ({ ...m, [key]: value as ScoreCategory }));
  }

  // --- Multipliers -------------------------------------------------------
  setMultiplier(index: number, value: string): void {
    this.multiplierRules.update((rules) =>
      rules.map((r, i) => (i === index ? { ...r, multiplier: this.toInt(value) } : r)),
    );
  }

  setClassicMultiplier(index: number, value: string): void {
    this.multiplierRules.update((rules) =>
      rules.map((r, i) => (i === index ? { ...r, classicMultiplier: this.toInt(value) } : r)),
    );
  }

  // --- Classic teams -----------------------------------------------------
  toggleTeam(teamId: string): void {
    this.teams.update((list) =>
      list.map((t) => (t.teamId === teamId ? { ...t, isClassic: !t.isClassic } : t)),
    );
  }

  // --- Save / recalculate ------------------------------------------------
  save(): void {
    const request: ScoringConfigRequest = {
      basePoints: { ...this.basePoints() },
      // Only persist scores assigned to a non-default category (ExtraUncommon is implicit).
      scoreEntries: this.scoreGrid()
        .map((c) => ({ low: c.low, high: c.high, category: this.category(c.key) }))
        .filter((e) => e.category !== ScoreCategory.ExtraUncommon),
      multiplierRules: this.multiplierRules().map((r) => ({ ...r })),
      classicTeamIds: this.teams()
        .filter((t) => t.isClassic)
        .map((t) => t.teamId),
    };

    this.saving.set(true);
    this.configApi
      .update(this.seasonId(), request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (config) => {
          this.applyConfig(config);
          this.saving.set(false);
          this.toast.success(this.translate.instant('scoringRules.saved'));
        },
        error: () => this.saving.set(false),
      });
  }

  recalculate(): void {
    this.recalculating.set(true);
    this.standingsApi
      .recalculate(this.seasonId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.recalculating.set(false);
          this.toast.success(this.translate.instant('scoringRules.recalculated'));
        },
        error: () => this.recalculating.set(false),
      });
  }

  private toInt(value: string): number {
    const n = Math.trunc(Number(value));
    return Number.isFinite(n) ? n : 0;
  }
}
