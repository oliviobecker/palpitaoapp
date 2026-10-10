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
import { Competition } from '@core/models/enums';
import { Team, TeamSyncResponse } from '@core/models';
import { ToastService } from '@core/notifications/toast.service';
import { TeamsService } from '@core/services/teams.service';
import { CompetitionBadge } from '@shared/components/competition-badge/competition-badge';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { ErrorState } from '@shared/components/error-state/error-state';
import { Icon } from '@shared/components/icon/icon';
import { PageHeader } from '@shared/components/page-header/page-header';
import { SkeletonList } from '@shared/components/skeleton/skeleton-list';
import { TEAM_DIVISIONS, filterTeams, groupTeams, diffTotals } from './admin-teams.util';

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
    this.teamsApi
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
    this.teamsApi
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
    this.teamsApi
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
