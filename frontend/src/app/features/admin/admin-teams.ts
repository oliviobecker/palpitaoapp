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
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Competition, TeamType } from '../../core/models/enums';
import { Team, TeamSyncResponse } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { TeamsService } from '../../core/services/teams.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';

/** The league divisions a club can be moved to; `null` means "no division". */
export const TEAM_DIVISIONS: Competition[] = [
  Competition.PremierLeague,
  Competition.Championship,
  Competition.LeagueOne,
];

/** Roster size each division is expected to have, shown next to the count as a review hint. */
export const EXPECTED_ROSTER: Partial<Record<Competition, number>> = {
  [Competition.PremierLeague]: 20,
  [Competition.Championship]: 24,
  [Competition.LeagueOne]: 24,
};

export type TeamGroupKey = Competition | 'unassigned' | 'national';

export interface TeamGroup {
  key: TeamGroupKey;
  /** Set for the league groups; null for the "no division" and national-team groups. */
  competition: Competition | null;
  teams: Team[];
  expected: number | null;
}

export function filterTeams(teams: Team[], query: string): Team[] {
  const term = query.trim().toLowerCase();
  if (!term) {
    return teams;
  }

  return teams.filter(
    (t) => t.name.toLowerCase().includes(term) || (t.shortName ?? '').toLowerCase().includes(term),
  );
}

/**
 * Groups the catalogue for review: the three divisions first (always shown, so an
 * empty one is visible rather than absent), then the clubs with no division, then
 * the national teams. A payload without `teamType` predates the field and is read
 * as a club.
 */
export function groupTeams(teams: Team[]): TeamGroup[] {
  const byName = (a: Team, b: Team) => a.name.localeCompare(b.name);
  const isNational = (t: Team) => t.teamType === TeamType.NationalTeam;

  const groups: TeamGroup[] = TEAM_DIVISIONS.map((competition) => ({
    key: competition,
    competition,
    teams: teams.filter((t) => !isNational(t) && t.division === competition).sort(byName),
    expected: EXPECTED_ROSTER[competition] ?? null,
  }));

  const unassigned = teams.filter((t) => !isNational(t) && !t.division).sort(byName);
  if (unassigned.length > 0) {
    groups.push({ key: 'unassigned', competition: null, teams: unassigned, expected: null });
  }

  const national = teams.filter(isNational).sort(byName);
  if (national.length > 0) {
    groups.push({ key: 'national', competition: null, teams: national, expected: null });
  }

  return groups;
}

export interface DiffTotals {
  created: number;
  moved: number;
  notFound: number;
  unchanged: number;
  found: number;
}

export function diffTotals(sync: TeamSyncResponse): DiffTotals {
  const sum = (pick: (c: TeamSyncResponse['competitions'][number]) => number) =>
    sync.competitions.reduce((acc, c) => acc + pick(c), 0);

  return {
    created: sum((c) => c.toCreate.length),
    moved: sum((c) => c.toMove.length),
    notFound: sum((c) => c.notFound.length),
    unchanged: sum((c) => c.unchangedCount),
    found: sum((c) => c.foundCount),
  };
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-teams',
  imports: [
    RouterLink,
    TranslatePipe,
    CompetitionBadge,
    EmptyState,
    ErrorState,
    Icon,
    PageHeader,
    SkeletonList,
  ],
  templateUrl: './admin-teams.html',
  styleUrl: './admin-teams.scss',
})
export class AdminTeams implements OnInit {
  private readonly teamsApi = inject(TeamsService);
  private readonly api = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly teams = signal<Team[]>([]);
  protected readonly search = signal('');
  /** Id of the row whose division is being saved, so only its select is disabled. */
  protected readonly busyId = signal<string | null>(null);
  protected readonly previewing = signal(false);
  protected readonly applying = signal(false);
  protected readonly preview = signal<TeamSyncResponse | null>(null);

  protected readonly divisions = TEAM_DIVISIONS;

  protected readonly groups = computed(() => groupTeams(filterTeams(this.teams(), this.search())));

  protected readonly totals = computed(() => {
    const p = this.preview();
    return p ? diffTotals(p) : null;
  });

  protected readonly hasChanges = computed(() => {
    const t = this.totals();
    return !!t && t.created + t.moved > 0;
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.teamsApi
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.teams.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  protected onDivisionChange(team: Team, raw: string): void {
    const division = (raw || null) as Competition | null;
    if ((team.division ?? null) === division) {
      return;
    }

    this.busyId.set(team.id);
    this.api
      .updateTeamDivision(team.id, division)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          // Patch the row in place; the grouping is computed, so it regroups itself.
          this.teams.update((list) => list.map((t) => (t.id === updated.id ? updated : t)));
          this.busyId.set(null);
          this.toast.success(
            division
              ? this.translate.instant('adminTeams.moved', {
                  name: updated.name,
                  division: this.translate.instant('fixtures.comp.' + division),
                })
              : this.translate.instant('adminTeams.movedNone', { name: updated.name }),
          );
        },
        error: () => {
          this.busyId.set(null);
          // The interceptor already toasted; reloading puts the select back on the
          // value the server actually holds.
          this.load();
        },
      });
  }

  protected syncPreview(): void {
    this.previewing.set(true);
    this.api
      .syncTeamsPreview()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.preview.set(response);
          this.previewing.set(false);
        },
        error: () => this.previewing.set(false),
      });
  }

  protected applySync(): void {
    this.applying.set(true);
    this.api
      .syncTeamsApply()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const totals = diffTotals(response);
          this.applying.set(false);
          this.preview.set(null);
          this.toast.success(this.translate.instant('adminTeams.applied', totals));
          this.load();
        },
        error: () => this.applying.set(false),
      });
  }

  protected cancelPreview(): void {
    this.preview.set(null);
  }
}
