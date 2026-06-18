import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TournamentType } from '../../core/models/enums';
import { Season } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { SeasonsService } from '../../core/services/seasons.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-seasons',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, EmptyState, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin">Admin</a> · {{ 'adminSeasons.title' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-0">{{ 'adminSeasons.title' | translate }}</h1>
    </div>

    <div class="card mb-3">
      <div class="card-body p-4">
        <div class="d-flex align-items-center gap-2 mb-3">
          <span class="icon-tile icon-tile--violet">📅</span>
          <h2 class="h6 fw-bold mb-0">
            {{ (editingId() ? 'adminSeasons.edit' : 'adminSeasons.new') | translate }}
          </h2>
        </div>
        <form [formGroup]="form" (ngSubmit)="save()" class="vstack gap-3">
          <div>
            <label class="form-label">{{ 'adminSeasons.name' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text">🏷️</span>
              <input
                class="form-control"
                [placeholder]="'adminSeasons.name' | translate"
                formControlName="name"
              />
            </div>
          </div>
          <div class="row g-3">
            <div class="col-6">
              <label class="form-label">{{ 'adminSeasons.start' | translate }}</label>
              <div class="input-group input-group-lg">
                <span class="input-group-text">📅</span>
                <input type="date" class="form-control" formControlName="startDate" />
              </div>
            </div>
            <div class="col-6">
              <label class="form-label">{{ 'adminSeasons.end' | translate }}</label>
              <div class="input-group input-group-lg">
                <span class="input-group-text">🗓️</span>
                <input type="date" class="form-control" formControlName="endDate" />
              </div>
            </div>
          </div>
          <div class="form-check">
            <input
              type="checkbox"
              class="form-check-input"
              id="active"
              formControlName="isActive"
            />
            <label class="form-check-label" for="active">{{
              'adminSeasons.active' | translate
            }}</label>
          </div>

          <div>
            <label class="form-label d-block">{{ 'tournamentType.label' | translate }}</label>
            <div class="row g-2">
              <div class="col-12 col-sm-6">
                <button
                  type="button"
                  class="btn btn-outline-secondary w-100 h-100 text-start p-3"
                  [class.active]="
                    form.controls.tournamentType.value === TournamentType.PalpitaoEngland
                  "
                  [disabled]="!!editingId()"
                  (click)="selectType(TournamentType.PalpitaoEngland)"
                >
                  <span class="fw-semibold d-block">{{
                    'tournamentType.palpitaoEngland' | translate
                  }}</span>
                  <span class="small d-block">{{ 'createGroupType.england' | translate }}</span>
                </button>
              </div>
              <div class="col-12 col-sm-6">
                <button
                  type="button"
                  class="btn btn-outline-secondary w-100 h-100 text-start p-3"
                  [class.active]="
                    form.controls.tournamentType.value === TournamentType.FifaWorldCup
                  "
                  [disabled]="!!editingId()"
                  (click)="selectType(TournamentType.FifaWorldCup)"
                >
                  <span class="fw-semibold d-block">{{
                    'tournamentType.fifaWorldCup' | translate
                  }}</span>
                  <span class="small d-block">{{ 'createGroupType.worldCup' | translate }}</span>
                </button>
              </div>
            </div>
            <div class="form-text">{{ 'adminSeasons.typeFixedHelp' | translate }}</div>
          </div>

          <hr class="my-1" />

          <div>
            <div class="fw-semibold mb-1">{{ 'predictionSubmission.modeLabel' | translate }}</div>
            <div class="form-check form-switch">
              <input
                type="checkbox"
                class="form-check-input"
                id="allowSubmit"
                formControlName="allowParticipantsToSubmitPredictions"
              />
              <label class="form-check-label" for="allowSubmit">{{
                'predictionSubmission.participantsCanSubmit' | translate
              }}</label>
            </div>
            <div class="form-text">{{ 'predictionSubmission.adminOnlyHelp' | translate }}</div>
            @if (
              editingHasPredictions() && !form.controls.allowParticipantsToSubmitPredictions.value
            ) {
              <div class="alert alert-warning py-2 small mt-2 mb-0" role="alert">
                {{ 'predictionSubmission.disableWarning' | translate }}
              </div>
            }
          </div>

          <div class="form-check form-switch">
            <input
              type="checkbox"
              class="form-check-input"
              id="allowView"
              formControlName="allowParticipantsToViewOthersPredictions"
            />
            <label class="form-check-label" for="allowView">{{
              'settings.allowParticipantsToViewOthersPredictions' | translate
            }}</label>
            <div class="form-text">
              {{ 'settings.allowParticipantsToViewOthersPredictionsHelp' | translate }}
            </div>
          </div>

          <div class="d-flex gap-2">
            <button
              type="submit"
              class="btn btn-primary btn-lg flex-fill"
              [disabled]="form.invalid || saving()"
            >
              {{ (editingId() ? 'common.save' : 'adminSeasons.create') | translate }}
            </button>
            @if (editingId()) {
              <button type="button" class="btn btn-outline-secondary btn-lg" (click)="resetForm()">
                {{ 'common.cancel' | translate }}
              </button>
            }
          </div>
        </form>
      </div>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (seasons().length === 0) {
      <app-empty-state [message]="'adminSeasons.empty' | translate" />
    } @else {
      <div class="vstack gap-2">
        @for (s of seasons(); track s.id) {
          <div class="card" [class.border-success]="s.isActive">
            <div class="card-body py-2 px-3 d-flex justify-content-between align-items-center">
              <div>
                <div class="fw-semibold">
                  {{ s.name }}
                  @if (s.isActive) {
                    <span class="badge text-bg-success ms-1">{{
                      'adminSeasons.activeBadge' | translate
                    }}</span>
                  }
                </div>
                <small class="text-muted">{{ s.startDate }} → {{ s.endDate }}</small>
              </div>
              <div class="d-flex gap-1">
                <button class="btn btn-sm btn-outline-secondary" (click)="edit(s)">
                  {{ 'common.edit' | translate }}
                </button>
                @if (!s.isActive) {
                  <button class="btn btn-sm btn-outline-success" (click)="activate(s)">
                    {{ 'adminSeasons.activate' | translate }}
                  </button>
                }
              </div>
            </div>
          </div>
        }
      </div>
    }
  `,
})
export class AdminSeasons implements OnInit {
  private readonly api = inject(SeasonsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  /** Exposed so the template can reference the enum members in the type selector. */
  protected readonly TournamentType = TournamentType;

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly seasons = signal<Season[]>([]);
  protected readonly editingId = signal<string | null>(null);

  protected readonly editingHasPredictions = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
    isActive: [false],
    tournamentType: [TournamentType.PalpitaoEngland as TournamentType, Validators.required],
    allowParticipantsToSubmitPredictions: [true],
    allowParticipantsToViewOthersPredictions: [false],
  });

  /** The certame type is fixed after creation; it can only be chosen while creating. */
  selectType(type: TournamentType): void {
    if (this.editingId()) {
      return;
    }
    this.form.controls.tournamentType.setValue(type);
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.seasons.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  edit(season: Season): void {
    this.editingId.set(season.id);
    this.editingHasPredictions.set(season.hasParticipantPredictions);
    this.form.setValue({
      name: season.name,
      startDate: season.startDate.substring(0, 10),
      endDate: season.endDate.substring(0, 10),
      isActive: season.isActive,
      tournamentType: season.tournamentType,
      allowParticipantsToSubmitPredictions: season.allowParticipantsToSubmitPredictions,
      allowParticipantsToViewOthersPredictions: season.allowParticipantsToViewOthersPredictions,
    });
  }

  resetForm(): void {
    this.editingId.set(null);
    this.editingHasPredictions.set(false);
    this.form.reset({
      name: '',
      startDate: '',
      endDate: '',
      isActive: false,
      tournamentType: TournamentType.PalpitaoEngland,
      allowParticipantsToSubmitPredictions: true,
      allowParticipantsToViewOthersPredictions: false,
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const value = this.form.getRawValue();
    const id = this.editingId();
    const request$ = id ? this.api.update(id, value) : this.api.create(value);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('adminSeasons.saved'));
        this.saving.set(false);
        this.resetForm();
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  activate(season: Season): void {
    this.api
      .activate(season.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('adminSeasons.activated'));
          this.load();
        },
      });
  }
}
