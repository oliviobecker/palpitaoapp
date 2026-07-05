import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AdminService } from '../../core/services/admin.service';
import { Icon } from '../../shared/components/icon/icon';

interface AdminTab {
  label: string;
  icon: string;
  link: string;
  exact: boolean;
  /** When true, shows the pending-requests count badge. */
  badge?: boolean;
}

/**
 * Layout wrapper for every `/admin/**` page: a persistent horizontal tab bar
 * (desktop only — mobile keeps the dashboard action-cards + bottom nav) so an
 * admin can jump between areas without returning to the panel. The pending
 * registration count is surfaced as a badge on the "Requests" tab.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe, Icon],
  template: `
    <nav class="admin-subnav d-none d-md-flex mb-3" aria-label="Admin">
      @for (t of tabs; track t.link) {
        <a
          class="admin-subnav__link"
          [routerLink]="t.link"
          routerLinkActive="is-active"
          [routerLinkActiveOptions]="{ exact: t.exact }"
        >
          <app-icon [name]="t.icon" [size]="16" />
          <span>{{ t.label | translate }}</span>
          @if (t.badge && pendingCount() > 0) {
            <span class="admin-subnav__badge">{{ pendingCount() }}</span>
          }
        </a>
      }
    </nav>

    <router-outlet />
  `,
  styles: [
    `
      .admin-subnav {
        flex-wrap: wrap;
        gap: 0.4rem;
        padding-bottom: 0.25rem;
      }
      .admin-subnav__link {
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
        text-decoration: none;
        transition:
          background 0.15s ease,
          border-color 0.15s ease,
          color 0.15s ease;
      }
      .admin-subnav__link:hover {
        background: var(--surface-2);
        color: var(--ink);
      }
      .admin-subnav__link.is-active {
        background: #161d2c;
        border-color: #161d2c;
        color: #fff;
      }
      .admin-subnav__badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-width: 1.2rem;
        height: 1.2rem;
        padding: 0 0.35rem;
        border-radius: 999px;
        background: var(--brand);
        color: #fff;
        font-size: 0.7rem;
        font-weight: 700;
      }
      .admin-subnav__link.is-active .admin-subnav__badge {
        background: #fff;
        color: #161d2c;
      }
    `,
  ],
})
export class AdminLayout implements OnInit {
  private readonly api = inject(AdminService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pendingCount = signal(0);

  protected readonly tabs: AdminTab[] = [
    { label: 'adminNav.panel', icon: 'house', link: '/admin', exact: true },
    { label: 'adminNav.seasons', icon: 'calendar-days', link: '/admin/seasons', exact: false },
    { label: 'adminNav.rounds', icon: 'list', link: '/admin/rounds', exact: false },
    { label: 'adminNav.scoring', icon: 'calculator', link: '/admin/scoring', exact: false },
    { label: 'adminNav.participants', icon: 'users', link: '/admin/participants', exact: false },
    {
      label: 'adminNav.requests',
      icon: 'clipboard-list',
      link: '/admin/registration-requests',
      exact: false,
      badge: true,
    },
    { label: 'adminNav.audit', icon: 'scroll-text', link: '/admin/audit', exact: false },
  ];

  ngOnInit(): void {
    // Best-effort: a failure just leaves the badge hidden.
    this.api
      .listRegistrationRequests()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (list) => this.pendingCount.set(list.length), error: () => {} });
  }
}
