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
  imports: [RouterLink, TranslatePipe, EmptyState, ErrorState, PageHeader, SkeletonList],
  templateUrl: './standings.html',
  styles: [
    `
      .podium {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 0.5rem;
      }
      .podium__slot {
        display: flex;
        flex-direction: column;
        align-items: center;
        text-align: center;
        gap: 0.25rem;
        padding: 0.9rem 0.5rem 0.7rem;
        background: var(--surface);
        border: 1px solid var(--border);
        border-top: 3px solid var(--medal, #cbd5e1);
        border-radius: var(--radius);
        box-shadow: var(--shadow-sm);
        min-width: 0;
      }
      .podium__slot--1 {
        --medal: #f5b301;
      }
      .podium__slot--2 {
        --medal: #9aa6b8;
      }
      .podium__slot--3 {
        --medal: #cd7f32;
      }
      .podium__slot--me {
        outline: 2px solid var(--brand);
        outline-offset: -1px;
      }
      .podium__rank {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 1.4rem;
        height: 1.4rem;
        border-radius: 999px;
        background: var(--medal);
        color: #fff;
        font-weight: 800;
        font-size: 0.8rem;
      }
      .podium__avatar,
      .rank-avatar {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        border-radius: 50%;
        color: #fff;
        font-weight: 700;
        flex: none;
      }
      .podium__avatar {
        width: 2.75rem;
        height: 2.75rem;
        font-size: 0.85rem;
      }
      .rank-avatar {
        width: 1.8rem;
        height: 1.8rem;
        font-size: 0.62rem;
      }
      .podium__name {
        font-weight: 600;
        font-size: 0.85rem;
        max-width: 100%;
      }
      .podium__pts {
        font-weight: 800;
      }
      .podium__pts small {
        font-weight: 600;
        color: var(--muted);
      }
    `,
  ],
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

  /** Top three for the podium; only shown when there are at least three players. */
  protected readonly podium = computed(() => this.standings().slice(0, 3));

  /** Initials for the avatar tile, e.g. "João Silva" → "JS". */
  initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    const first = parts[0]?.[0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
    return (first + last).toUpperCase();
  }

  /** Deterministic colour per name (mirrors the dashboard standings preview). */
  avatarColor(name: string): string {
    let hash = 0;
    for (const ch of name) {
      hash = (hash * 31 + ch.charCodeAt(0)) % 360;
    }
    return `hsl(${hash}, 52%, 42%)`;
  }

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
