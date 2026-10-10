import { AdminAbsencesService } from '@core/services/admin/admin-absences.service';
import { AdminUsersService } from '@core/services/admin/admin-users.service';
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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Observable, catchError, of } from 'rxjs';
import { HasUnsavedChanges } from '@core/guards/unsaved-changes.guard';
import { Absence, AbsenceCandidateRound, AbsenceReviewRound, Participant } from '@core/models';
import { ConfirmChoice, ConfirmService } from '@core/notifications/confirm.service';
import { ToastService } from '@core/notifications/toast.service';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { ErrorState } from '@shared/components/error-state/error-state';
import { FormField } from '@shared/components/form-field/form-field';
import { Icon } from '@shared/components/icon/icon';
import { PageHeader } from '@shared/components/page-header/page-header';
import { SkeletonList } from '@shared/components/skeleton/skeleton-list';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { roundLabel } from '@shared/utils/round-name.util';
import {
  isPreselectedAbsence,
  toAbsenceReviewDecisions,
  absenceReviewHintKey,
  absenceReviewToastKey,
} from './admin-participants.util';
import { strongPassword } from '@shared/validators/password.validators';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-participants',
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
    RoundLabelPipe,
  ],
  templateUrl: './admin-participants.html',
})
export class AdminParticipants implements OnInit, HasUnsavedChanges {
  private readonly absencesApi = inject(AdminAbsencesService);
  private readonly usersApi = inject(AdminUsersService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly saving = signal(false);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly editingId = signal<string | null>(null);
  protected readonly absences = signal<Record<string, Absence[]>>({});
  protected readonly search = signal('');

  /** Client-side filter by name or e-mail (lists are small — no server paging needed). */
  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.participants();
    return this.participants().filter(
      (p) => p.name.toLowerCase().includes(term) || p.email.toLowerCase().includes(term),
    );
  });

  protected readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    // Same strength rule as self-registration and the backend: 8+ chars, letter + digit.
    password: ['', strongPassword],
  });

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
    this.usersApi
      .listParticipants()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.participants.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  edit(p: Participant): void {
    this.editingId.set(p.id);
    this.form.controls.password.clearValidators();
    this.form.controls.password.updateValueAndValidity();
    this.form.patchValue({ name: p.name, email: p.email });
  }

  resetForm(): void {
    this.editingId.set(null);
    this.form.controls.password.setValidators(strongPassword);
    this.form.reset({ name: '', email: '', password: '' });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { name, email, password } = this.form.getRawValue();
    const id = this.editingId();
    const request$ = id
      ? this.usersApi.updateParticipant(id, { name, email })
      : this.usersApi.createParticipant({ name, email, password });
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('adminParticipants.saved'));
        this.saving.set(false);
        this.resetForm();
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  setActive(p: Participant, active: boolean): void {
    if (!active) {
      // Deactivating never records absences, so it stays a one-click action.
      this.usersApi
        .deactivateParticipant(p.id)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({ next: () => this.afterAction('adminParticipants.deactivatedMsg') });
      return;
    }
    this.candidates(p.id).subscribe({ next: (rounds) => void this.confirmActivate(p, rounds) });
  }

  /**
   * With nothing to record, activation stays a one-click action; otherwise the admin picks
   * which of the rounds that closed while they were out should count as an absence.
   */
  private async confirmActivate(p: Participant, rounds: AbsenceCandidateRound[]): Promise<void> {
    let absentRoundIds: string[] = [];
    if (rounds.length > 0) {
      const answer = await this.confirm.askWithChoices(
        this.translate.instant('adminParticipants.confirmActivateAbsences', { name: p.name }),
        rounds.map((r) => this.toChoice(r)),
        {
          title: this.translate.instant('adminParticipants.activate'),
          confirmText: this.translate.instant('adminParticipants.activate'),
          choicesLabel: this.translate.instant('adminParticipants.absentRoundsLabel'),
        },
      );
      if (answer === null) return;
      absentRoundIds = answer.choiceIds;
    }
    this.usersApi
      .activateParticipant(p.id, absentRoundIds)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.afterAction('adminParticipants.activatedMsg') });
  }

  async eliminate(p: Participant): Promise<void> {
    const justification = await this.confirm.askWithInput(
      this.translate.instant('adminParticipants.confirmEliminate', { name: p.name }),
      {
        title: this.translate.instant('adminParticipants.eliminate'),
        confirmText: this.translate.instant('adminParticipants.eliminate'),
        danger: true,
        inputLabel: this.translate.instant('adminParticipants.promptEliminate'),
      },
    );
    if (justification === null) return;
    this.usersApi
      .eliminateParticipant(p.id, justification)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.afterAction('adminParticipants.eliminatedMsg') });
  }

  reactivate(p: Participant): void {
    this.candidates(p.id).subscribe({ next: (rounds) => void this.confirmReactivate(p, rounds) });
  }

  /** The justification is always required, so this dialog opens even with no candidates. */
  private async confirmReactivate(p: Participant, rounds: AbsenceCandidateRound[]): Promise<void> {
    const answer = await this.confirm.askWithChoices(
      this.translate.instant('adminParticipants.confirmReactivate', { name: p.name }),
      rounds.map((r) => this.toChoice(r)),
      {
        title: this.translate.instant('adminParticipants.reactivate'),
        confirmText: this.translate.instant('adminParticipants.reactivate'),
        choicesLabel: this.translate.instant('adminParticipants.absentRoundsLabel'),
        withInput: true,
        inputLabel: this.translate.instant('adminParticipants.promptReactivate'),
      },
    );
    if (answer === null) return;
    this.absencesApi
      .reactivate(p.id, answer.text, answer.choiceIds)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.afterAction('adminParticipants.reactivatedMsg') });
  }

  /** catchError before takeUntilDestroyed, so the empty fallback still reaches the subscriber. */
  private candidates(userId: string): Observable<AbsenceCandidateRound[]> {
    return this.absencesApi.getAbsenceCandidateRounds(userId).pipe(
      catchError(() => of<AbsenceCandidateRound[]>([])),
      takeUntilDestroyed(this.destroyRef),
    );
  }

  private toChoice(r: AbsenceCandidateRound): ConfirmChoice {
    return {
      id: r.roundId,
      label: this.choiceLabel(r),
      hint: r.hasPresentOverride
        ? this.translate.instant('adminParticipants.absentRoundHasPresentOverride')
        : r.requiresRescore
          ? this.translate.instant('adminParticipants.absentRoundNeedsRescore')
          : undefined,
      checked: isPreselectedAbsence(r),
    };
  }

  private choiceLabel(r: { number: number; part?: number; title?: string | null }): string {
    const number = roundLabel(r.number, r.part);
    return r.title
      ? this.translate.instant('adminParticipants.absentRoundOptionTitled', {
          number,
          title: r.title,
        })
      : this.translate.instant('adminParticipants.absentRoundOption', { number });
  }

  /**
   * Excuse (or restore) absences in closed rounds — e.g. rounds played before the participant
   * actually joined. An excused round stays at 0 points but leaves the absence ladder.
   */
  reviewAbsences(p: Participant): void {
    this.absencesApi
      .getAbsenceReviewRounds(p.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (rounds) => void this.confirmReview(p, rounds) });
  }

  /**
   * Ticked = still counts as absent, unticked = present. The boxes open in today's state, so
   * confirming the dialog untouched is a guaranteed no-op; a change to an already-scored round
   * replays the season on the server, which the message warns about.
   */
  private async confirmReview(p: Participant, rounds: AbsenceReviewRound[]): Promise<void> {
    if (rounds.length === 0) {
      this.toast.info(this.translate.instant('adminParticipants.reviewNothing'));
      return;
    }
    let message = this.translate.instant('adminParticipants.confirmReviewAbsences', {
      name: p.name,
    });
    if (rounds.some((r) => r.requiresRecalculation)) {
      message += ' ' + this.translate.instant('adminParticipants.reviewRecalcWarning');
    }
    // A round played in parts is one round for absences: every part has to stay ticked.
    if (rounds.some((r) => (r.part ?? 0) > 0)) {
      message += ' ' + this.translate.instant('adminParticipants.reviewPartsHint');
    }
    const answer = await this.confirm.askWithChoices(
      message,
      rounds.map((r) => this.toReviewChoice(r)),
      {
        title: this.translate.instant('adminParticipants.reviewAbsences'),
        confirmText: this.translate.instant('adminParticipants.reviewAbsences'),
        choicesLabel: this.translate.instant('adminParticipants.reviewRoundsLabel'),
        withInput: true,
        inputLabel: this.translate.instant('adminParticipants.promptReviewAbsences'),
      },
    );
    if (answer === null) return;
    this.absencesApi
      .reviewAbsences(p.id, answer.text, toAbsenceReviewDecisions(rounds, answer.choiceIds))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.refreshAbsences(p);
          this.afterAction(absenceReviewToastKey(result));
        },
      });
  }

  private toReviewChoice(r: AbsenceReviewRound): ConfirmChoice {
    return {
      id: r.roundId,
      label: this.choiceLabel(r),
      hint: this.translate.instant(absenceReviewHintKey(r), {
        n: r.absenceNumber,
        penalty: r.penaltyPoints,
      }),
      checked: r.isAbsent,
    };
  }

  toggleAbsences(p: Participant): void {
    const current = this.absences();
    if (current[p.id]) {
      const copy = { ...current };
      delete copy[p.id];
      this.absences.set(copy);
      return;
    }
    this.loadAbsences(p);
  }

  /** An expanded absence list is stale after a review; a collapsed one has nothing to refresh. */
  private refreshAbsences(p: Participant): void {
    if (this.absences()[p.id]) {
      this.loadAbsences(p);
    }
  }

  private loadAbsences(p: Participant): void {
    this.absencesApi
      .getUserAbsences(p.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => this.absences.set({ ...this.absences(), [p.id]: list }),
      });
  }

  private afterAction(key: string): void {
    this.toast.success(this.translate.instant(key));
    this.load();
  }
}
