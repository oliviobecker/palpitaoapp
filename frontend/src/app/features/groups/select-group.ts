import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { GroupRole } from '../../core/models/enums';
import { MyGroup } from '../../core/models/models';
import { GroupContextService } from '../../core/services/group-context.service';
import { GroupsService } from '../../core/services/groups.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-select-group',
  imports: [TranslatePipe],
  template: `
    <div class="container py-4" style="max-width: 560px;">
      <div class="text-center mb-4">
        <div class="auth-logo">⚽</div>
        <h1 class="h4 fw-bold mb-1">{{ 'selectGroup.title' | translate }}</h1>
        <p class="text-muted small mb-0">{{ 'selectGroup.subtitle' | translate }}</p>
      </div>

      @if (auth.isAdmin()) {
        <div class="alert alert-warning py-2 small" role="alert">
          {{ 'superAdmin.selectGroupHint' | translate }}
        </div>
      }

      @if (loading()) {
        <div class="text-center text-muted py-4">
          <span class="spinner-border spinner-border-sm me-2"></span>
        </div>
      } @else if (groups().length === 0) {
        <div class="alert alert-warning" role="alert">
          {{ 'group.noApprovedAccess' | translate }}
        </div>
      } @else {
        <div class="list-group">
          @for (g of groups(); track g.groupId) {
            <button
              type="button"
              class="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
              (click)="enter(g)"
            >
              <span class="fw-semibold">{{ g.groupName }}</span>
              <span class="badge text-bg-secondary">{{ 'groupRole.' + g.role | translate }}</span>
            </button>
          }
        </div>
      }

      <div class="text-center mt-4">
        <button type="button" class="btn btn-link btn-sm text-muted" (click)="logout()">
          {{ 'nav.logout' | translate }}
        </button>
      </div>
    </div>
  `,
})
export class SelectGroup implements OnInit {
  private readonly groupsApi = inject(GroupsService);
  private readonly groupContext = inject(GroupContextService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly groups = signal<MyGroup[]>([]);

  ngOnInit(): void {
    this.groupsApi
      .myGroups()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (groups) => {
          this.groups.set(groups);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  enter(group: MyGroup): void {
    this.groupContext.select(group);
    this.router.navigate([group.role === GroupRole.GroupAdmin ? '/admin' : '/dashboard']);
  }

  logout(): void {
    this.groupContext.clear();
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
