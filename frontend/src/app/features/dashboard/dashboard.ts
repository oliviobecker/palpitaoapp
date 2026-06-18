import { DatePipe } from '@angular/common';
import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { forkJoin, of, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { RoundStatus } from '../../core/models/enums';
import {
  MyPredictions,
  Prediction,
  Round,
  RoundMatch,
  RoundSummary,
  Standing,
} from '../../core/models/models';
import { PredictionsService } from '../../core/services/predictions.service';
import { RoundsService } from '../../core/services/rounds.service';
import { SeasonsService } from '../../core/services/seasons.service';
import { StandingsService } from '../../core/services/standings.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { Skeleton } from '../../shared/components/skeleton/skeleton';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe, TranslatePipe, CompetitionBadge, ErrorState, Skeleton, Icon],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit, OnDestroy {
  private readonly roundsApi = inject(RoundsService);
  private readonly predictionsApi = inject(PredictionsService);
  private readonly standingsApi = inject(StandingsService);
  private readonly seasonsApi = inject(SeasonsService);
  protected readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly openRound = signal<Round | null>(null);
  protected readonly lockedRound = signal<RoundSummary | null>(null);
  protected readonly myPredictions = signal<MyPredictions | null>(null);
  protected readonly standings = signal<Standing[]>([]);
  protected readonly seasonName = signal<string | null>(null);

  protected readonly currentUserId = computed(() => this.auth.currentUser()?.id ?? null);
  protected readonly myStanding = computed(
    () => this.standings().find((s) => s.userId === this.currentUserId()) ?? null,
  );
  protected readonly topStandings = computed(() => this.standings().slice(0, 4));

  protected readonly matchCount = computed(() => this.openRound()?.matches.length ?? 0);
  protected readonly predictionsDone = computed(
    () => this.myPredictions()?.predictions.length ?? 0,
  );
  protected readonly allPredicted = computed(
    () => this.matchCount() > 0 && this.predictionsDone() === this.matchCount(),
  );
  protected readonly progressPct = computed(() =>
    this.matchCount() > 0 ? Math.round((this.predictionsDone() / this.matchCount()) * 100) : 0,
  );
  protected readonly deadline = computed(() => this.openRound()?.firstMatchStartsAt ?? null);
  protected readonly nextMatches = computed<RoundMatch[]>(() =>
    [...(this.openRound()?.matches ?? [])]
      .sort((a, b) => a.startsAt.localeCompare(b.startsAt))
      .slice(0, 4),
  );

  // --- Live countdown -------------------------------------------------------
  private readonly now = signal(Date.now());
  private timer?: ReturnType<typeof setInterval>;
  protected readonly countdown = computed(() => {
    const d = this.deadline();
    const ms = d ? new Date(d).getTime() - this.now() : 0;
    const total = Math.max(0, Math.floor(ms / 1000));
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return {
      days: pad(Math.floor(total / 86400)),
      hours: pad(Math.floor((total % 86400) / 3600)),
      mins: pad(Math.floor((total % 3600) / 60)),
      secs: pad(total % 60),
    };
  });

  // --- Per-match prediction lookup -----------------------------------------
  private readonly predictionMap = computed(() => {
    const map = new Map<string, Prediction>();
    for (const p of this.myPredictions()?.predictions ?? []) {
      map.set(p.roundMatchId, p);
    }
    return map;
  });
  protected predictionFor(matchId: string): Prediction | undefined {
    return this.predictionMap().get(matchId);
  }

  /** Two-letter initials for the standings avatar. */
  initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    const first = parts[0]?.[0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
    return (first + last).toUpperCase();
  }

  /** Deterministic colour per name for the avatar. */
  avatarColor(name: string): string {
    let hash = 0;
    for (const ch of name) {
      hash = (hash * 31 + ch.charCodeAt(0)) % 360;
    }
    return `hsl(${hash}, 52%, 45%)`;
  }

  ngOnInit(): void {
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
    this.load();
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.roundsApi
      .getAll()
      .pipe(
        switchMap((list) => {
          const open = list.find((r) => r.status === RoundStatus.Published);
          this.lockedRound.set(list.find((r) => r.status === RoundStatus.Locked) ?? null);

          // Standings belong to the group's active season (certame); deriving it
          // from rounds[0] picked the wrong certame when a group had more than one.
          return this.seasonsApi.getActive().pipe(
            switchMap((season) =>
              forkJoin({
                detail: open ? this.roundsApi.getById(open.id) : of(null),
                mine: open ? this.predictionsApi.getMine(open.id) : of(null),
                standings: season ? this.standingsApi.getStandings(season.id) : of([]),
                season: of(season),
              }),
            ),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ detail, mine, standings, season }) => {
          this.openRound.set(detail);
          this.myPredictions.set(mine);
          this.standings.set(standings);
          this.seasonName.set(season?.name ?? null);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }
}
