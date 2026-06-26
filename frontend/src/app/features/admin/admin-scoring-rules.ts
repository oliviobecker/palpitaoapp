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
import {
  Competition,
  ENGLAND_PHASES,
  MatchPhase,
  ScoreCategory,
  WORLD_CUP_PHASES,
} from '../../core/models/enums';
import {
  ScoringConfig,
  ScoringConfigRequest,
  ScoringConfigTeam,
  ScoringMultiplierRule,
  Season,
} from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
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
import { HasUnsavedChanges } from '../../core/guards/unsaved-changes.guard';

const MAX_GOALS = 6;

interface BasePointsForm {
  columnOnly: number;
  traditional: number;
  medium: number;
  uncommon: number;
  extraUncommon: number;
}

interface CategoryMeta {
  cat: ScoreCategory;
  field: keyof BasePointsForm;
  rgb: string;
}

/** Palette categories (also the base-points legend), in display order. */
const CATEGORIES: CategoryMeta[] = [
  { cat: ScoreCategory.ColumnOnly, field: 'columnOnly', rgb: '148,163,184' },
  { cat: ScoreCategory.Traditional, field: 'traditional', rgb: '59,130,246' },
  { cat: ScoreCategory.Medium, field: 'medium', rgb: '34,197,94' },
  { cat: ScoreCategory.Uncommon, field: 'uncommon', rgb: '245,158,11' },
  { cat: ScoreCategory.ExtraUncommon, field: 'extraUncommon', rgb: '239,68,68' },
];

/** Stable competition order for the multiplier groups. */
const COMPETITION_ORDER: Competition[] = [
  Competition.PremierLeague,
  Competition.FACup,
  Competition.Championship,
  Competition.LeagueOne,
  Competition.FifaWorldCup,
];

/** Stable phase order within a competition group. */
const PHASE_ORDER: MatchPhase[] = [...ENGLAND_PHASES, ...WORLD_CUP_PHASES];

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-scoring-rules',
  imports: [TranslatePipe, CompetitionBadge, ErrorState, Icon, Loading, PageHeader],
  templateUrl: './admin-scoring-rules.html',
  styleUrl: './admin-scoring-rules.scss',
})
export class AdminScoringRules implements OnInit, HasUnsavedChanges {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly configApi = inject(ScoringConfigService);
  private readonly standingsApi = inject(StandingsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly confirm = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly categories = CATEGORIES;
  protected readonly range = Array.from({ length: MAX_GOALS + 1 }, (_, i) => i);
  protected readonly phaseLabel = phaseLabel;

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly saving = signal(false);
  protected readonly recalculating = signal(false);
  protected readonly dirty = signal(false);

  protected readonly seasons = signal<Season[]>([]);
  protected readonly seasonId = signal('');
  protected readonly config = signal<ScoringConfig | null>(null);

  // Editable state.
  protected readonly basePoints = signal<BasePointsForm>({
    columnOnly: 0,
    traditional: 0,
    medium: 0,
    uncommon: 0,
    extraUncommon: 0,
  });
  /** "low-high" → category for the score grid (ExtraUncommon = unmapped default). */
  protected readonly scoreCategories = signal<Record<string, ScoreCategory>>({});
  protected readonly multiplierRules = signal<ScoringMultiplierRule[]>([]);
  protected readonly teams = signal<ScoringConfigTeam[]>([]);
  protected readonly teamFilter = signal('');
  /** Active "brush" category — painting a grid cell assigns it. */
  protected readonly activeCategory = signal<ScoreCategory>(ScoreCategory.Traditional);

  protected readonly hasScoredRounds = computed(() => this.config()?.hasScoredRounds ?? false);
  protected readonly selectedTeams = computed(() => this.teams().filter((t) => t.isClassic));
  protected readonly addableTeams = computed(() => {
    const q = this.teamFilter().trim().toLowerCase();
    return this.teams()
      .filter((t) => !t.isClassic)
      .filter((t) => !q || t.name.toLowerCase().includes(q));
  });

  /** Multiplier rules grouped by competition, in stable competition/phase order. */
  protected readonly multiplierGroups = computed(() => {
    const byComp = new Map<Competition, { rule: ScoringMultiplierRule; index: number }[]>();
    this.multiplierRules().forEach((rule, index) => {
      const list = byComp.get(rule.competition) ?? [];
      list.push({ rule, index });
      byComp.set(rule.competition, list);
    });
    return COMPETITION_ORDER.filter((c) => byComp.has(c)).map((competition) => ({
      competition,
      rows: byComp
        .get(competition)!
        .sort((a, b) => PHASE_ORDER.indexOf(a.rule.phase) - PHASE_ORDER.indexOf(b.rule.phase)),
    }));
  });

  // --- Category helpers --------------------------------------------------
  key(home: number, away: number): string {
    return `${Math.min(home, away)}-${Math.max(home, away)}`;
  }

  categoryAt(key: string): ScoreCategory {
    return this.scoreCategories()[key] ?? ScoreCategory.ExtraUncommon;
  }

  rgb(cat: ScoreCategory): string {
    return CATEGORIES.find((c) => c.cat === cat)?.rgb ?? '148,163,184';
  }

  border(cat: ScoreCategory): string {
    return `rgb(${this.rgb(cat)})`;
  }

  fill(cat: ScoreCategory): string {
    return `rgba(${this.rgb(cat)}, 0.16)`;
  }

  basePointsFor(cat: ScoreCategory): number {
    const field = CATEGORIES.find((c) => c.cat === cat)?.field;
    return field ? this.basePoints()[field] : 0;
  }

  categoryCount(cat: ScoreCategory): number {
    let count = 0;
    for (let low = 0; low <= MAX_GOALS; low++) {
      for (let high = low; high <= MAX_GOALS; high++) {
        if (this.categoryAt(`${low}-${high}`) === cat) count++;
      }
    }
    return count;
  }

  paint(home: number, away: number): void {
    const key = this.key(home, away);
    this.scoreCategories.update((m) => ({ ...m, [key]: this.activeCategory() }));
    this.markDirty();
  }

  // --- Base points -------------------------------------------------------
  stepBase(field: keyof BasePointsForm, delta: number): void {
    this.basePoints.update((bp) => ({ ...bp, [field]: Math.max(0, bp[field] + delta) }));
    this.markDirty();
  }

  setBaseValue(field: keyof BasePointsForm, value: string): void {
    this.basePoints.update((bp) => ({ ...bp, [field]: this.clamp(value, 0) }));
    this.markDirty();
  }

  // --- Multipliers -------------------------------------------------------
  stepMultiplier(index: number, delta: number): void {
    this.multiplierRules.update((rules) =>
      rules.map((r, i) =>
        i === index ? { ...r, multiplier: Math.max(1, r.multiplier + delta) } : r,
      ),
    );
    this.markDirty();
  }

  stepClassic(index: number, delta: number): void {
    this.multiplierRules.update((rules) =>
      rules.map((r, i) =>
        i === index ? { ...r, classicMultiplier: Math.max(1, r.classicMultiplier + delta) } : r,
      ),
    );
    this.markDirty();
  }

  setMultiplierValue(index: number, value: string): void {
    this.multiplierRules.update((rules) =>
      rules.map((r, i) => (i === index ? { ...r, multiplier: this.clamp(value, 1) } : r)),
    );
    this.markDirty();
  }

  setClassicValue(index: number, value: string): void {
    this.multiplierRules.update((rules) =>
      rules.map((r, i) => (i === index ? { ...r, classicMultiplier: this.clamp(value, 1) } : r)),
    );
    this.markDirty();
  }

  /** Parses a numeric input value and clamps to an integer ≥ min. */
  private clamp(value: string, min: number): number {
    const n = Math.trunc(Number(value));
    return Number.isFinite(n) ? Math.max(min, n) : min;
  }

  // --- Classic teams -----------------------------------------------------
  setTeam(teamId: string, isClassic: boolean): void {
    this.teams.update((list) => list.map((t) => (t.teamId === teamId ? { ...t, isClassic } : t)));
    this.markDirty();
  }

  // --- Lifecycle ---------------------------------------------------------
  ngOnInit(): void {
    this.loadSeasons();
  }

  /** Used by the unsaved-changes route guard. */
  hasUnsavedChanges(): boolean {
    return this.dirty();
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
    this.dirty.set(false);
  }

  private markDirty(): void {
    this.dirty.set(true);
  }

  // --- Save / recalculate ------------------------------------------------
  save(): void {
    const request: ScoringConfigRequest = {
      basePoints: { ...this.basePoints() },
      // Persist only scores assigned to a non-default category (ExtraUncommon is implicit).
      scoreEntries: this.normalizedEntries().filter(
        (e) => e.category !== ScoreCategory.ExtraUncommon,
      ),
      multiplierRules: this.multiplierRules().map((r) => ({ ...r })),
      classicTeamIds: this.selectedTeams().map((t) => t.teamId),
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

  async recalculate(): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('scoringRules.confirmRecalculate'), {
      title: this.translate.instant('scoringRules.recalculate'),
      confirmText: this.translate.instant('scoringRules.recalculate'),
      danger: true,
    });
    if (!ok) {
      return;
    }
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

  private normalizedEntries(): { low: number; high: number; category: ScoreCategory }[] {
    const entries: { low: number; high: number; category: ScoreCategory }[] = [];
    for (let low = 0; low <= MAX_GOALS; low++) {
      for (let high = low; high <= MAX_GOALS; high++) {
        entries.push({ low, high, category: this.categoryAt(`${low}-${high}`) });
      }
    }
    return entries;
  }
}
