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
import { TranslatePipe } from '@ngx-translate/core';
import { of, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Standing } from '../../core/models/models';
import { SeasonsService } from '../../core/services/seasons.service';
import { StandingsService } from '../../core/services/standings.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-standings',
  imports: [TranslatePipe, EmptyState, ErrorState, PageHeader, SkeletonList],
  templateUrl: './standings.html',
})
export class Standings implements OnInit {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly standingsApi = inject(StandingsService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly standings = signal<Standing[]>([]);
  protected readonly myId = computed(() => this.auth.currentUser()?.id);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    // Standings belong to the group's active season (certame). Deriving the
    // season from rounds[0] picked the wrong certame whenever a group had more
    // than one, leaving the table empty for the certame actually being scored.
    this.loading.set(true);
    this.error.set(false);
    this.seasonsApi
      .getActive()
      .pipe(
        switchMap((season) => (season ? this.standingsApi.getStandings(season.id) : of(null))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (rows) => {
          if (rows) {
            this.standings.set(rows);
          }
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }
}
