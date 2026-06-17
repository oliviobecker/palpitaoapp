import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditLog } from '../../core/models/models';
import { AdminService, AuditFilter } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  selector: 'app-admin-audit',
  imports: [FormsModule, DatePipe, RouterLink, TranslatePipe, EmptyState, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin">Admin</a> · {{ 'adminAudit.title' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-0">{{ 'adminAudit.title' | translate }}</h1>
    </div>

    <div class="card mb-3">
      <div class="card-body p-4 vstack gap-3">
        <div class="input-group input-group-lg">
          <span class="input-group-text">🗂️</span>
          <select class="form-select" [(ngModel)]="entityName">
            <option value="">{{ 'adminAudit.allEntities' | translate }}</option>
            @for (e of entities; track e) {
              <option [value]="e">{{ e }}</option>
            }
          </select>
        </div>
        <div class="row g-3">
          <div class="col-6">
            <label class="form-label">{{ 'adminAudit.from' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text">📅</span>
              <input type="date" class="form-control" [(ngModel)]="from" />
            </div>
          </div>
          <div class="col-6">
            <label class="form-label">{{ 'adminAudit.to' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text">🗓️</span>
              <input type="date" class="form-control" [(ngModel)]="to" />
            </div>
          </div>
        </div>
        <button class="btn btn-primary btn-lg w-100" (click)="apply()">
          🔎 {{ 'adminAudit.filter' | translate }}
        </button>
      </div>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (logs().length === 0) {
      <app-empty-state [message]="'adminAudit.empty' | translate" />
    } @else {
      <div class="vstack gap-2">
        @for (log of logs(); track log.id) {
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
    }
  `,
})
export class AdminAudit implements OnInit {
  private readonly api = inject(AdminService);

  protected readonly entities = ['Round', 'RoundMatch', 'Prediction', 'User', 'Season'];
  protected readonly loading = signal(true);
  protected readonly logs = signal<AuditLog[]>([]);

  protected entityName = '';
  protected from = '';
  protected to = '';

  ngOnInit(): void {
    this.apply();
  }

  apply(): void {
    this.loading.set(true);
    const filter: AuditFilter = {};
    if (this.entityName) filter.entityName = this.entityName;
    if (this.from) filter.from = `${this.from}T00:00:00`;
    if (this.to) filter.to = `${this.to}T23:59:59`;

    this.api.getAuditLogs(filter).subscribe({
      next: (list) => {
        this.logs.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
