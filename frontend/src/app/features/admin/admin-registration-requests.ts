import { DatePipe } from '@angular/common';
import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { RegistrationRequest } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-registration-requests',
  imports: [FormsModule, DatePipe, RouterLink, TranslatePipe, EmptyState, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin">Admin</a> · {{ 'adminRegistrations.title' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-0">{{ 'adminRegistrations.title' | translate }}</h1>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (requests().length === 0) {
      <app-empty-state [message]="'adminRegistrations.empty' | translate" />
    } @else {
      <div class="vstack gap-2">
        @for (r of requests(); track r.id) {
          <div class="card">
            <div class="card-body py-2 px-3">
              <div class="fw-semibold">{{ r.name }}</div>
              <small class="text-muted d-block">{{ r.email }}</small>
              <small class="text-muted">
                {{ 'adminRegistrations.requestedAt' | translate }}:
                {{ r.createdAt | date: 'dd/MM/yyyy HH:mm' }}
                <span class="badge text-bg-warning ms-1">{{
                  'adminRegistrations.pending' | translate
                }}</span>
              </small>

              @if (rejectingId() === r.id) {
                <div class="vstack gap-2 mt-2 border-top pt-2">
                  <input
                    class="form-control"
                    [placeholder]="'adminRegistrations.reasonPlaceholder' | translate"
                    [(ngModel)]="reason"
                  />
                  <div class="d-flex gap-2">
                    <button
                      class="btn btn-danger flex-fill"
                      [disabled]="busyId() === r.id"
                      (click)="confirmReject(r)"
                    >
                      {{ 'adminRegistrations.confirmReject' | translate }}
                    </button>
                    <button class="btn btn-outline-secondary" (click)="cancelReject()">
                      {{ 'common.cancel' | translate }}
                    </button>
                  </div>
                </div>
              } @else {
                <div class="d-flex gap-2 mt-2">
                  <button
                    class="btn btn-success flex-fill btn-lg"
                    [disabled]="busyId() === r.id"
                    (click)="approve(r)"
                  >
                    ✓ {{ 'adminRegistrations.approve' | translate }}
                  </button>
                  <button
                    class="btn btn-outline-danger flex-fill btn-lg"
                    [disabled]="busyId() === r.id"
                    (click)="startReject(r)"
                  >
                    ✕ {{ 'adminRegistrations.reject' | translate }}
                  </button>
                </div>
              }
            </div>
          </div>
        }
      </div>
    }
  `,
})
export class AdminRegistrationRequests implements OnInit {
  private readonly api = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly requests = signal<RegistrationRequest[]>([]);
  protected readonly busyId = signal<string | null>(null);
  protected readonly rejectingId = signal<string | null>(null);
  protected reason = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api
      .listRegistrationRequests()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.requests.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  async approve(r: RegistrationRequest): Promise<void> {
    const ok = await this.confirm.ask(
      this.translate.instant('adminRegistrations.confirmApproveMsg', { name: r.name }),
      { title: this.translate.instant('adminRegistrations.approve') },
    );
    if (!ok) return;

    this.busyId.set(r.id);
    this.api
      .approveRegistration(r.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('adminRegistrations.approvedMsg'));
          this.busyId.set(null);
          this.load();
        },
        error: () => this.busyId.set(null),
      });
  }

  startReject(r: RegistrationRequest): void {
    this.reason = '';
    this.rejectingId.set(r.id);
  }

  cancelReject(): void {
    this.rejectingId.set(null);
    this.reason = '';
  }

  confirmReject(r: RegistrationRequest): void {
    this.busyId.set(r.id);
    this.api
      .rejectRegistration(r.id, this.reason.trim() || undefined)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('adminRegistrations.rejectedMsg'));
          this.busyId.set(null);
          this.rejectingId.set(null);
          this.reason = '';
          this.load();
        },
        error: () => this.busyId.set(null),
      });
  }
}
