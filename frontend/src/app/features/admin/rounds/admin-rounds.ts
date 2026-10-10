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
import { RoundStatus } from '@core/models/enums';
import { RoundSummary } from '@core/models';
import { RoundsService } from '@core/services/rounds.service';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { ErrorState } from '@shared/components/error-state/error-state';
import { Icon } from '@shared/components/icon/icon';
import { PageHeader } from '@shared/components/page-header/page-header';
import { SkeletonList } from '@shared/components/skeleton/skeleton-list';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { compareRounds } from '@shared/utils/round-name.util';

type Filter = 'all' | RoundStatus;

const STATUS_ORDER: RoundStatus[] = [
  RoundStatus.Draft,
  RoundStatus.Published,
  RoundStatus.Locked,
  RoundStatus.Scored,
  RoundStatus.Cancelled,
];

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-rounds',
  imports: [
    RouterLink,
    TranslatePipe,
    EmptyState,
    ErrorState,
    Icon,
    PageHeader,
    SkeletonList,
    RoundLabelPipe,
  ],
  template: `
    <app-page-header
      [trail]="'Admin · ' + ('adminRounds.title' | translate)"
      [title]="'adminRounds.title' | translate"
    >
      <a class="btn btn-success" routerLink="/admin/rounds/new"
        ><app-icon name="plus" [size]="16" /> {{ 'adminRounds.new' | translate }}</a
      >
    </app-page-header>

    @if (loading()) {
      <app-skeleton-list />
    } @else if (error()) {
      <app-error-state (retry)="load()" />
    } @else if (rounds().length === 0) {
      <app-empty-state icon="list" [message]="'adminRounds.empty' | translate">
        <a class="btn btn-success btn-sm mt-1" routerLink="/admin/rounds/new">
          <app-icon name="plus" [size]="16" /> {{ 'adminRounds.new' | translate }}
        </a>
      </app-empty-state>
    } @else {
      <div class="round-tabs mb-3">
        @for (t of tabs(); track t.key) {
          <button
            type="button"
            class="round-tab"
            [class.is-active]="filter() === t.key"
            (click)="filter.set(t.key)"
          >
            {{ t.label | translate }} <span class="round-tab__count">{{ t.count }}</span>
          </button>
        }
      </div>

      <div class="vstack gap-2">
        @for (r of filtered(); track r.id) {
          <a
            class="card round-item r--{{ statusKey(r.status) }}"
            [routerLink]="['/admin/rounds', r.id]"
          >
            <span class="round-tile">
              <span class="round-tile__label">{{ 'adminRounds.tile' | translate }}</span>
              <span class="round-tile__num" [class.round-tile__num--part]="!!r.part">{{
                r.number | roundLabel: r.part
              }}</span>
            </span>
            <div class="round-item__body">
              <div class="fw-semibold text-truncate">
                {{ 'dashboard.round' | translate }} {{ r.number | roundLabel: r.part }}
                @if (r.title) {
                  · {{ r.title }}
                }
              </div>
              <small class="text-muted"
                ><app-icon name="clock" [size]="13" /> {{ r.matchCount }}
                {{ 'adminRounds.games' | translate }}</small
              >
            </div>
            <span class="round-pill">{{ 'status.' + r.status | translate }}</span>
          </a>
        }
      </div>
    }
  `,
  styleUrl: './admin-rounds.scss',
})
export class AdminRounds implements OnInit {
  private readonly api = inject(RoundsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly rounds = signal<RoundSummary[]>([]);
  protected readonly filter = signal<Filter>('all');

  private readonly sorted = computed(() => [...this.rounds()].sort((a, b) => compareRounds(b, a)));

  protected readonly filtered = computed(() => {
    const f = this.filter();
    return f === 'all' ? this.sorted() : this.sorted().filter((r) => r.status === f);
  });

  /** "All N" plus one tab per status that actually has rounds. */
  protected readonly tabs = computed(() => {
    const list = this.rounds();
    const tabs: { key: Filter; label: string; count: number }[] = [
      { key: 'all', label: 'adminRounds.all', count: list.length },
    ];
    for (const status of STATUS_ORDER) {
      const count = list.filter((r) => r.status === status).length;
      if (count > 0) {
        tabs.push({ key: status, label: `adminRounds.filter.${status}`, count });
      }
    }
    return tabs;
  });

  protected statusKey(status: RoundStatus): string {
    return status.toLowerCase();
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.rounds.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }
}
