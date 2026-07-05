import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { GroupUserStatus } from '../../core/models/enums';
import { MyGroup } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { GroupContextService } from '../../core/services/group-context.service';
import { GroupsService } from '../../core/services/groups.service';
import { Icon } from '../../shared/components/icon/icon';

/**
 * Shown after login when the account is valid but has no approved group yet. Lists
 * the user's pending (or rejected) memberships and lets them re-check (in case the
 * group admin just approved) or log out.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-awaiting-approval',
  imports: [TranslatePipe, Icon],
  template: `
    <div class="container py-4" style="max-width: 560px;">
      <div class="text-center mb-4">
        <div class="auth-logo"><app-icon name="hourglass" [size]="26" class="text-white" /></div>
        <h1 class="h4 fw-bold mb-1">{{ 'awaitingApproval.title' | translate }}</h1>
        <p class="text-muted small mb-0">{{ 'awaitingApproval.intro' | translate }}</p>
      </div>

      @if (loading()) {
        <div class="text-center text-muted py-4">
          <span class="spinner-border spinner-border-sm me-2"></span>
        </div>
      } @else if (error()) {
        <div class="alert alert-danger" role="alert">{{ 'errors.loadFailed' | translate }}</div>
      } @else if (pending().length === 0) {
        <div class="alert alert-warning" role="alert">
          {{ 'awaitingApproval.noMemberships' | translate }}
        </div>
      } @else {
        <div class="list-group">
          @for (g of pending(); track g.groupId) {
            <div class="list-group-item d-flex justify-content-between align-items-center">
              <span class="fw-semibold">{{ g.groupName }}</span>
              @if (g.status === GroupUserStatus.Rejected) {
                <span class="badge text-bg-danger">{{
                  'awaitingApproval.rejectedBadge' | translate
                }}</span>
              } @else if (!g.isActive) {
                <span class="badge text-bg-secondary">{{
                  'awaitingApproval.deactivatedBadge' | translate
                }}</span>
              } @else {
                <span class="badge text-bg-warning">{{
                  'awaitingApproval.pendingBadge' | translate
                }}</span>
              }
            </div>
          }
        </div>
      }

      <div class="d-grid gap-2 mt-4">
        <button type="button" class="btn btn-primary" (click)="recheck()" [disabled]="rechecking()">
          @if (rechecking()) {
            <span class="spinner-border spinner-border-sm me-2"></span>
          }
          {{ 'awaitingApproval.recheck' | translate }}
        </button>
        <p class="text-muted small text-center mb-0">
          <app-icon name="refresh-cw" [size]="13" /> {{ 'awaitingApproval.autoHint' | translate }}
        </p>
        <button type="button" class="btn btn-link btn-sm text-muted" (click)="logout()">
          {{ 'nav.logout' | translate }}
        </button>
      </div>
    </div>
  `,
})
export class AwaitingApproval implements OnInit, OnDestroy {
  private readonly groupsApi = inject(GroupsService);
  private readonly groupContext = inject(GroupContextService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly GroupUserStatus = GroupUserStatus;
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly rechecking = signal(false);
  protected readonly pending = signal<MyGroup[]>([]);

  /** Silent poll so an approved user is let in without having to press the button. */
  private pollTimer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.load();
    this.pollTimer = setInterval(() => this.recheck(true), 30_000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.groupsApi
      .pendingGroups()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (groups) => {
          this.pending.set(groups);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  /**
   * Re-check approved access: enter if approved now, otherwise stay on this screen.
   * `silent` is used by the background poll — it skips the spinner and the
   * "still pending" toast so the automatic checks are unobtrusive.
   */
  recheck(silent = false): void {
    if (!silent) {
      this.rechecking.set(true);
    }
    this.groupsApi
      .myGroups()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (groups) => {
          this.rechecking.set(false);
          if (groups.length === 1) {
            this.groupContext.select(groups[0]);
            this.router.navigate([this.groupContext.homePath(groups[0].role)]);
          } else if (groups.length > 1) {
            this.router.navigate(['/select-group']);
          } else if (!silent) {
            this.toast.info(this.translate.instant('awaitingApproval.stillPending'));
            this.load();
          }
        },
        error: () => this.rechecking.set(false),
      });
  }

  logout(): void {
    this.groupContext.clear();
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
