import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { RoundStatus } from '../../core/models/enums';
import { RoundSummary } from '../../core/models/models';
import { RoundsService } from '../../core/services/rounds.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

type Filter = 'all' | RoundStatus;

const STATUS_ORDER: RoundStatus[] = [
  RoundStatus.Draft,
  RoundStatus.Published,
  RoundStatus.Locked,
  RoundStatus.Scored,
  RoundStatus.Cancelled,
];

@Component({
  selector: 'app-admin-rounds',
  imports: [RouterLink, TranslatePipe, EmptyState, Loading],
  template: `
    <div class="d-flex justify-content-between align-items-start gap-2 mb-3 flex-wrap">
      <div>
        <div class="page-trail">Admin · {{ 'adminRounds.title' | translate }}</div>
        <h1 class="h4 fw-bold mb-0">{{ 'adminRounds.title' | translate }}</h1>
      </div>
      <a class="btn btn-success" routerLink="/admin/rounds/new"
        >➕ {{ 'adminRounds.new' | translate }}</a
      >
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (rounds().length === 0) {
      <app-empty-state [message]="'adminRounds.empty' | translate" />
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
              <span class="round-tile__num">{{ r.number }}</span>
            </span>
            <div class="round-item__body">
              <div class="fw-semibold text-truncate">
                {{ 'dashboard.round' | translate }} {{ r.number }}
                @if (r.title) {
                  · {{ r.title }}
                }
              </div>
              <small class="text-muted"
                >🕐 {{ r.matchCount }} {{ 'adminRounds.games' | translate }}</small
              >
            </div>
            <span class="round-pill">{{ 'status.' + r.status | translate }}</span>
          </a>
        }
      </div>
    }
  `,
  styles: [
    `
      .round-tabs {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
      .round-tab {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        border: 1px solid var(--border);
        background: var(--surface);
        color: var(--ink-soft);
        font-weight: 600;
        font-size: 0.85rem;
        padding: 0.4rem 0.9rem;
        border-radius: 999px;
        cursor: pointer;
        transition:
          background 0.15s ease,
          border-color 0.15s ease;
      }
      .round-tab:hover {
        background: var(--surface-2);
      }
      .round-tab.is-active {
        background: #161d2c;
        border-color: #161d2c;
        color: #fff;
      }
      .round-tab__count {
        font-size: 0.72rem;
        opacity: 0.7;
      }

      .round-item {
        display: flex;
        flex-direction: row;
        align-items: center;
        gap: 0.9rem;
        padding: 0.85rem 1rem;
        text-decoration: none;
        color: var(--ink);
        border-left: 4px solid var(--c, #64748b);
        transition:
          box-shadow 0.15s ease,
          transform 0.12s ease;
      }
      .round-item:hover {
        box-shadow: var(--shadow);
        transform: translateY(-1px);
        color: var(--ink);
      }
    `,
  ],
})
export class AdminRounds implements OnInit {
  private readonly api = inject(RoundsService);

  protected readonly loading = signal(true);
  protected readonly rounds = signal<RoundSummary[]>([]);
  protected readonly filter = signal<Filter>('all');

  private readonly sorted = computed(() => [...this.rounds()].sort((a, b) => b.number - a.number));

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
    this.api.getAll().subscribe({
      next: (list) => {
        this.rounds.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
