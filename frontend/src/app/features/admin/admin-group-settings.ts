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
      @if (saved()) {
        <div class="alert alert-success py-2" role="alert">
          {{ 'adminGroupSettings.saved' | translate }}
        </div>
      }

      <!-- Prediction submission mode -->
      <div class="card mb-3">
        <div class="card-body">
          <div class="fw-semibold mb-1">{{ 'predictionSubmission.modeLabel' | translate }}</div>
          <div class="form-text mb-2">
            {{ 'predictionSubmission.adminOnlyHelp' | translate }}
          </div>

          <div class="form-check form-switch">
            <input
              id="allowSubmit"
              type="checkbox"
              class="form-check-input"
              [ngModel]="allowSubmit()"
              (ngModelChange)="onToggleSubmit($event)"
              [disabled]="saving()"
            />
            <label class="form-check-label" for="allowSubmit">{{
              'predictionSubmission.participantsCanSubmit' | translate
            }}</label>
          </div>

          @if (!allowSubmit() && hasParticipantPredictions()) {
            <div class="alert alert-warning py-2 small mt-2 mb-0" role="alert">
              {{ 'predictionSubmission.disableWarning' | translate }}
            </div>
          }
        </div>
      </div>

      <!-- Mirror visibility -->
      <div class="card mb-3">
        <div class="card-body">
          <div class="form-check form-switch">
            <input
              id="allowViewOthers"
              type="checkbox"
              class="form-check-input"
              [ngModel]="allowView()"
              (ngModelChange)="onToggleView($event)"
              [disabled]="saving()"
            />
            <label class="form-check-label fw-semibold" for="allowViewOthers">{{
              'settings.allowParticipantsToViewOthersPredictions' | translate
            }}</label>
          </div>
          <div class="form-text">
            {{ 'settings.allowParticipantsToViewOthersPredictionsHelp' | translate }}
          </div>
        </div>
      </div>

      <button type="button" class="btn btn-primary" (click)="save()" [disabled]="saving()">
        @if (saving()) {
          <span class="spinner-border spinner-border-sm me-2"></span>
        }
        {{ 'adminGroupSettings.save' | translate }}
      </button>
    }
  `,
})
export class AdminGroupSettings implements OnInit {
  private readonly adminApi = inject(AdminService);
  private readonly group = inject(GroupContextService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly allowView = signal(false);
  protected readonly allowSubmit = signal(true);
  protected readonly hasParticipantPredictions = signal(false);

  ngOnInit(): void {
    this.adminApi.getGroupSettings().subscribe({
      next: (s) => {
        this.allowView.set(s.allowParticipantsToViewOthersPredictions);
        this.allowSubmit.set(s.allowParticipantsToSubmitPredictions);
        this.hasParticipantPredictions.set(s.hasParticipantPredictions);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onToggleView(value: boolean): void {
    this.allowView.set(value);
    this.saved.set(false);
  }

  onToggleSubmit(value: boolean): void {
    this.allowSubmit.set(value);
    this.saved.set(false);
  }

  save(): void {
    this.saving.set(true);
    this.saved.set(false);
    this.adminApi
      .updateGroupSettings({
        allowParticipantsToViewOthersPredictions: this.allowView(),
        allowParticipantsToSubmitPredictions: this.allowSubmit(),
      })
      .subscribe({
        next: (s) => {
          this.saving.set(false);
          this.saved.set(true);
          // Keep the cached group context in sync so the participant UI updates.
          this.group.setAllowViewOthersPredictions(s.allowParticipantsToViewOthersPredictions);
          this.group.setAllowParticipantsToSubmit(s.allowParticipantsToSubmitPredictions);
        },
        error: () => this.saving.set(false),
      });
  }
}
