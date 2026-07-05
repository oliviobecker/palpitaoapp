import { DatePipe } from '@angular/common';
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
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditLog } from '../../core/models/models';
import { AdminService, AuditFilter } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-audit',
  imports: [
    FormsModule,
    DatePipe,
    RouterLink,
    TranslatePipe,
    EmptyState,
    ErrorState,
    Icon,
    PageHeader,
    SkeletonList,
  ],
  template: `
    <app-page-header [title]="'adminAudit.title' | translate">
      <div trail class="page-trail">
        <a routerLink="/admin">Admin</a> · {{ 'adminAudit.title' | translate }}
      </div>
    </app-page-header>

    <div class="card mb-3">
      <div class="card-body p-4 vstack gap-3">
        <div class="input-group input-group-lg">
          <span class="input-group-text"><app-icon name="folder" [size]="16" /></span>
          <select class="form-select" [(ngModel)]="entityName">
            <option value="">{{ 'adminAudit.allEntities' | translate }}</option>
            @for (e of entities; track e) {
              <option [value]="e">{{ e }}</option>
            }
          </select>
        </div>
        <div class="row g-3">
          <div class="col-6">
            <label for="audit-from" class="form-label">{{ 'adminAudit.from' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text"><app-icon name="calendar-days" [size]="16" /></span>
              <input id="audit-from" type="date" class="form-control" [(ngModel)]="from" />
            </div>
          </div>
          <div class="col-6">
            <label for="audit-to" class="form-label">{{ 'adminAudit.to' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text"><app-icon name="calendar-days" [size]="16" /></span>
              <input id="audit-to" type="date" class="form-control" [(ngModel)]="to" />
            </div>
          </div>
        </div>
        <button class="btn btn-primary btn-lg w-100" (click)="apply()">
          <app-icon name="search" [size]="16" /> {{ 'adminAudit.filter' | translate }}
        </button>
      </div>
    </div>

    @if (loading()) {
      <app-skeleton-list [count]="6" />
    } @else if (error()) {
      <app-error-state (retry)="apply()" />
    } @else if (logs().length === 0) {
      <app-empty-state [message]="'adminAudit.empty' | translate" />
    } @else {
      <div class="vstack gap-2">
        @for (log of visibleLogs(); track log.id) {
          <div class="card">
            <div class="card-body py-2 px-3">
              <div class="d-flex justify-content-between">
                <span class="fw-semibold">{{ log.action }}</span>
                <small class="text-muted">{{ log.createdAt | date: 'dd/MM HH:mm' }}</small>
              </div>
              <div class="small text-muted">
                {{ log.userName ?? ('adminAudit.system' | translate) }} · {{ log.entityName }}
                @if (log.entityId) {
                  #{{ log.entityId.substring(0, 8) }}
                }
              </div>
              @if (log.details) {
                <div class="small text-body-secondary mt-1">
                  <code>{{ log.details }}</code>
                </div>
              }
            </div>
          </div>
        }
      </div>
      @if (logs().length > visibleCount()) {
        <button class="btn btn-outline-secondary w-100 mt-3" (click)="showMore()">
          {{ 'adminAudit.loadMore' | translate }}
          <span class="badge text-bg-light ms-1">{{ logs().length - visibleCount() }}</span>
        </button>
      }
    }
  `,
})
export class AdminAudit implements OnInit {
  private readonly api = inject(AdminService);
  private readonly destroyRef = inject(DestroyRef);

  /** How many rows to reveal per "load more" click. */
  private static readonly PAGE = 20;

  protected readonly entities = ['Round', 'RoundMatch', 'Prediction', 'User', 'Season'];
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly logs = signal<AuditLog[]>([]);
  protected readonly visibleCount = signal(AdminAudit.PAGE);
  protected readonly visibleLogs = computed(() => this.logs().slice(0, this.visibleCount()));

  protected entityName = '';
  protected from = '';
  protected to = '';

  ngOnInit(): void {
    this.apply();
  }

  showMore(): void {
    this.visibleCount.update((n) => n + AdminAudit.PAGE);
  }

  apply(): void {
    this.loading.set(true);
    this.visibleCount.set(AdminAudit.PAGE);
    const filter: AuditFilter = {};
    if (this.entityName) filter.entityName = this.entityName;
    if (this.from) filter.from = `${this.from}T00:00:00`;
    if (this.to) filter.to = `${this.to}T23:59:59`;

    this.error.set(false);
    this.api
      .getAuditLogs(filter)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.logs.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }
}
