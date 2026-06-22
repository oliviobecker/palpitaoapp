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
import { Absence, Participant } from '../../core/models/models';
import { ConfirmService } from '../../core/notifications/confirm.service';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { Loading } from '../../shared/components/loading/loading';
import { PageHeader } from '../../shared/components/page-header/page-header';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-participants',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    EmptyState,
    ErrorState,
    Icon,
    Loading,
    PageHeader,
  ],
  template: `
    <app-page-header [title]="'adminParticipants.title' | translate">
      <div trail class="page-trail">
        <a routerLink="/admin">Admin</a> · {{ 'adminParticipants.title' | translate }}
      </div>
    </app-page-header>

    <div class="card mb-3">
      <div class="card-body p-4">
        <div class="d-flex align-items-center gap-2 mb-3">
          <span class="icon-tile icon-tile--teal"><app-icon name="users" [size]="20" /></span>
          <h2 class="h6 fw-bold mb-0">
            {{ (editingId() ? 'adminParticipants.edit' : 'adminParticipants.new') | translate }}
          </h2>
        </div>
        <form [formGroup]="form" (ngSubmit)="save()" class="vstack gap-3">
          <div class="input-group input-group-lg">
            <span class="input-group-text"><app-icon name="user" [size]="16" /></span>
            <input
              class="form-control"
              [placeholder]="'adminParticipants.name' | translate"
              formControlName="name"
            />
          </div>
          <div class="input-group input-group-lg">
            <span class="input-group-text"><app-icon name="mail" [size]="16" /></span>
            <input
              class="form-control"
              [placeholder]="'adminParticipants.email' | translate"
              formControlName="email"
            />
          </div>
          @if (!editingId()) {
            <div class="input-group input-group-lg">
              <span class="input-group-text"><app-icon name="lock" [size]="16" /></span>
              <input
                type="password"
                class="form-control"
                [placeholder]="'adminParticipants.password' | translate"
                formControlName="password"
              />
            </div>
          }
          <div class="d-flex gap-2">
            <button
              type="submit"
              class="btn btn-primary btn-lg flex-fill"
              [disabled]="form.invalid || saving()"
            >
              {{ (editingId() ? 'common.save' : 'adminParticipants.create') | translate }}
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
    } @else if (error()) {
      <app-error-state (retry)="load()" />
    } @else if (participants().length === 0) {
      <app-empty-state [message]="'adminParticipants.empty' | translate" />
    } @else {
      <div class="vstack gap-2">
        @for (p of participants(); track p.id) {
          <div class="card" [class.border-danger]="p.isEliminated">
            <div class="card-body py-2 px-3">
              <div class="d-flex justify-content-between align-items-start">
                <div>
                  <div class="fw-semibold">
                    {{ p.name }}
                    @if (p.isEliminated) {
                      <span class="badge text-bg-danger ms-1">{{
                        'adminParticipants.eliminated' | translate
                      }}</span>
                    } @else if (!p.isActive) {
                      <span class="badge text-bg-secondary ms-1">{{
                        'adminParticipants.inactive' | translate
                      }}</span>
                    } @else {
                      <span class="badge text-bg-success ms-1">{{
                        'adminParticipants.active' | translate
                      }}</span>
                    }
                  </div>
                  <small class="text-muted d-block">{{ p.email }}</small>
                  <small class="text-muted">
                    {{ p.totalPoints }} {{ 'adminParticipants.points' | translate }} ·
                    {{ 'adminParticipants.absences' | translate }}: {{ p.absenceCount }} ·
                    {{ 'adminParticipants.penalties' | translate }}: {{ p.penaltyPoints }}
                  </small>
                </div>
              </div>

              <div class="d-flex flex-wrap gap-1 mt-2">
                <button class="btn btn-sm btn-outline-secondary" (click)="edit(p)">
                  {{ 'common.edit' | translate }}
                </button>
                @if (p.isActive) {
                  <button class="btn btn-sm btn-outline-warning" (click)="setActive(p, false)">
                    {{ 'adminParticipants.deactivate' | translate }}
                  </button>
                } @else {
                  <button class="btn btn-sm btn-outline-success" (click)="setActive(p, true)">
                    {{ 'adminParticipants.activate' | translate }}
                  </button>
                }
                @if (p.isEliminated) {
                  <button class="btn btn-sm btn-outline-success" (click)="reactivate(p)">
                    {{ 'adminParticipants.reactivate' | translate }}
                  </button>
                } @else {
                  <button class="btn btn-sm btn-outline-danger" (click)="eliminate(p)">
                    {{ 'adminParticipants.eliminate' | translate }}
                  </button>
                }
                <button class="btn btn-sm btn-outline-primary" (click)="toggleAbsences(p)">
                  {{ 'adminParticipants.absencesBtn' | translate }}
                </button>
              </div>

              @if (absences()[p.id]; as list) {
                <ul class="list-unstyled small mt-2 mb-0 border-top pt-2">
                  @if (list.length === 0) {
                    <li class="text-muted">{{ 'adminParticipants.noAbsences' | translate }}</li>
                  } @else {
                    @for (a of list; track a.roundId) {
                      <li>
                        {{ 'adminParticipants.round' | translate }} {{ a.roundNumber }} —
                        {{
                          'adminParticipants.absenceLine'
                            | translate: { n: a.absenceNumber, penalty: a.penaltyPoints }
                        }}
                      </li>
                    }
                  }
                </ul>
              }
            </div>
          </div>
        }
      </div>
    }
  `,
})
export class AdminParticipants implements OnInit {
  private readonly api = inject(AdminService);
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

  protected readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    // Same strength rule as self-registration and the backend: 8+ chars, letter + digit.
    password: ['', [Validators.required, Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).{8,}$/)]],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api
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
    this.form.controls.password.setValidators([
      Validators.required,
      Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).{8,}$/),
    ]);
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
      ? this.api.updateParticipant(id, { name, email })
      : this.api.createParticipant({ name, email, password });
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
    const call = active ? this.api.activateParticipant(p.id) : this.api.deactivateParticipant(p.id);
    call.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () =>
        this.afterAction(
          active ? 'adminParticipants.activatedMsg' : 'adminParticipants.deactivatedMsg',
        ),
    });
  }

  async eliminate(p: Participant): Promise<void> {
    const ok = await this.confirm.ask(
      this.translate.instant('adminParticipants.confirmEliminate', { name: p.name }),
      {
        title: this.translate.instant('adminParticipants.eliminate'),
        confirmText: this.translate.instant('adminParticipants.eliminate'),
        danger: true,
      },
    );
    if (!ok) return;
    const justification =
      window.prompt(this.translate.instant('adminParticipants.promptEliminate')) ?? '';
    if (!justification.trim()) {
      this.toast.error(this.translate.instant('adminParticipants.justificationRequired'));
      return;
    }
    this.api
      .eliminateParticipant(p.id, justification)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.afterAction('adminParticipants.eliminatedMsg') });
  }

  reactivate(p: Participant): void {
    const justification =
      window.prompt(this.translate.instant('adminParticipants.promptReactivate')) ?? '';
    if (!justification.trim()) {
      this.toast.error(this.translate.instant('adminParticipants.justificationRequired'));
      return;
    }
    this.api
      .reactivate(p.id, justification)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.afterAction('adminParticipants.reactivatedMsg') });
  }

  toggleAbsences(p: Participant): void {
    const current = this.absences();
    if (current[p.id]) {
      const copy = { ...current };
      delete copy[p.id];
      this.absences.set(copy);
      return;
    }
    this.api
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
