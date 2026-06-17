import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { GroupContextService } from '../../core/services/group-context.service';
import { AdminService } from '../../core/services/admin.service';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  selector: 'app-admin-group-settings',
  imports: [FormsModule, TranslatePipe, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">{{ 'adminDash.panel' | translate }} · Admin</div>
      <h1 class="h4 fw-bold mb-0">{{ 'adminGroupSettings.title' | translate }}</h1>
    </div>

    @if (loading()) {
      <app-loading />
    } @else {
      <div class="card">
        <div class="card-body">
          @if (saved()) {
            <div class="alert alert-success py-2" role="alert">
              {{ 'adminGroupSettings.saved' | translate }}
            </div>
          }

          <div class="form-check form-switch">
            <input
              id="allowViewOthers"
              type="checkbox"
              class="form-check-input"
              [ngModel]="allow()"
              (ngModelChange)="onToggle($event)"
              [disabled]="saving()"
            />
            <label class="form-check-label fw-semibold" for="allowViewOthers">{{
              'settings.allowParticipantsToViewOthersPredictions' | translate
            }}</label>
          </div>
          <div class="form-text mb-3">
            {{ 'settings.allowParticipantsToViewOthersPredictionsHelp' | translate }}
          </div>

          <button type="button" class="btn btn-primary" (click)="save()" [disabled]="saving()">
            @if (saving()) {
              <span class="spinner-border spinner-border-sm me-2"></span>
            }
            {{ 'adminGroupSettings.save' | translate }}
          </button>
        </div>
      </div>
    }
  `,
})
export class AdminGroupSettings implements OnInit {
  private readonly adminApi = inject(AdminService);
  private readonly group = inject(GroupContextService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly allow = signal(false);

  ngOnInit(): void {
    this.adminApi.getGroupSettings().subscribe({
      next: (s) => {
        this.allow.set(s.allowParticipantsToViewOthersPredictions);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onToggle(value: boolean): void {
    this.allow.set(value);
    this.saved.set(false);
  }

  save(): void {
    this.saving.set(true);
    this.saved.set(false);
    this.adminApi.updateGroupSettings(this.allow()).subscribe({
      next: (s) => {
        this.saving.set(false);
        this.saved.set(true);
        // Keep the cached group context in sync so the participant UI updates.
        this.group.setAllowViewOthersPredictions(s.allowParticipantsToViewOthersPredictions);
      },
      error: () => this.saving.set(false),
    });
  }
}
