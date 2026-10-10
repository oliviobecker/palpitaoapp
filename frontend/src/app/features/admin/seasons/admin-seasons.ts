import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HasUnsavedChanges } from '@core/guards/unsaved-changes.guard';
import { TournamentType } from '@core/models/enums';
import { Season } from '@core/models';
import { ConfirmService } from '@core/notifications/confirm.service';
import { ToastService } from '@core/notifications/toast.service';
import { SeasonsService } from '@core/services/seasons.service';
import { copyToClipboard } from '@shared/utils/clipboard.util';
import { publicStandingsUrl } from '@shared/utils/public-link.util';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { ErrorState } from '@shared/components/error-state/error-state';
import { FormField } from '@shared/components/form-field/form-field';
import { Icon } from '@shared/components/icon/icon';
import { PageHeader } from '@shared/components/page-header/page-header';
import { SkeletonList } from '@shared/components/skeleton/skeleton-list';

/** Form-level validator: endDate must not be before startDate (yyyy-MM-dd strings). */
function dateRange(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value;
  const end = group.get('endDate')?.value;
  return start && end && end < start ? { dateRange: true } : null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-seasons',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    EmptyState,
    ErrorState,
    FormField,
    Icon,
    PageHeader,
    SkeletonList,
  ],
  templateUrl: './admin-seasons.html',
})
export class AdminSeasons implements OnInit, HasUnsavedChanges {
  private readonly api = inject(SeasonsService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  /** Exposed so the template can reference the enum members in the type selector. */
  protected readonly TournamentType = TournamentType;

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly saving = signal(false);
  protected readonly seasons = signal<Season[]>([]);
  protected readonly editingId = signal<string | null>(null);

  protected readonly editingHasPredictions = signal(false);

  protected readonly form = this.fb.nonNullable.group(
    {
      name: ['', Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      isActive: [false],
      tournamentType: [TournamentType.PalpitaoEngland as TournamentType, Validators.required],
      allowParticipantsToSubmitPredictions: [true],
      allowParticipantsToViewOthersPredictions: [false],
      // England certames only; left on (and hidden) for the World Cup, which never
      // allows the FA Cup anyway.
      faCupEnabled: [true],
      publicStandingsEnabled: [false],
    },
    { validators: dateRange },
  );

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

  /** Used by the unsaved-changes route guard. */
  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.seasons.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
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
      faCupEnabled: season.faCupEnabled ?? true,
      publicStandingsEnabled: season.publicStandingsEnabled ?? false,
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
      faCupEnabled: true,
      publicStandingsEnabled: false,
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

  publicUrl(season: Season): string {
    return publicStandingsUrl(season.publicKey);
  }

  async copyLink(season: Season): Promise<void> {
    await copyToClipboard(this.publicUrl(season));
    this.toast.success(this.translate.instant('adminSeasons.linkCopied'));
  }

  async regenerate(season: Season): Promise<void> {
    const ok = await this.confirm.ask(this.translate.instant('adminSeasons.regenerateConfirm'), {
      title: this.translate.instant('adminSeasons.regenerateKey'),
      confirmText: this.translate.instant('adminSeasons.regenerateKey'),
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.api
      .regeneratePublicKey(season.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('adminSeasons.keyRegenerated'));
          this.load();
        },
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
