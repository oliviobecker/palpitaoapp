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
import { RoundsService } from '../../core/services/rounds.service';
import { StandingsService } from '../../core/services/standings.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-standings',
  imports: [TranslatePipe, EmptyState, Loading],
  templateUrl: './standings.html',
})
export class Standings implements OnInit {
  private readonly roundsApi = inject(RoundsService);
  private readonly standingsApi = inject(StandingsService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly standings = signal<Standing[]>([]);
  protected readonly myId = computed(() => this.auth.currentUser()?.id);

  ngOnInit(): void {
    // The active season is derived from the existing rounds.
    this.roundsApi
      .getAll()
      .pipe(
        switchMap((rounds) => {
          const seasonId = rounds[0]?.seasonId;
          return seasonId ? this.standingsApi.getStandings(seasonId) : of(null);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (rows) => {
          if (rows) {
            this.standings.set(rows);
          }
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
