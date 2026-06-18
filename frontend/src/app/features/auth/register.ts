import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { FormBuilder } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Lang, LanguageService } from '../../core/i18n/language.service';
import { PublicGroup } from '../../core/models/models';
import { httpErrorMessage } from '../../core/notifications/http-error';
import { AuthService } from '../../core/auth/auth.service';
import { GroupsService } from '../../core/services/groups.service';

/** Form-level validator: confirmPassword must equal password. */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="auth-wrapper">
      <div class="auth-card">
        <div>
          <div class="d-flex justify-content-end">
            <div class="btn-group btn-group-sm" role="group" aria-label="Language">
              <button
                type="button"
                class="btn btn-outline-secondary"
                [class.active]="language.current() === 'pt-BR'"
                (click)="setLanguage('pt-BR')"
              >
                PT
              </button>
              <button
                type="button"
                class="btn btn-outline-secondary"
                [class.active]="language.current() === 'en-US'"
                (click)="setLanguage('en-US')"
              >
                EN
              </button>
            </div>
          </div>

          <div class="text-center mb-4">
            <div class="auth-logo">⚽</div>
            <h1 class="h4 fw-bold mb-1">{{ 'app.name' | translate }}</h1>
            <p class="text-muted small mb-0">{{ 'register.title' | translate }}</p>
          </div>

          @if (success()) {
            <div class="alert alert-success" role="alert">{{ success() }}</div>
            <a class="btn btn-primary btn-lg w-100" routerLink="/login">{{
              'register.goToLogin' | translate
            }}</a>
          } @else {
            @if (error()) {
              <div class="alert alert-danger py-2" role="alert">{{ error() }}</div>
            }

            <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
              <div class="mb-3">
                <label for="groupId" class="form-label">{{ 'register.group' | translate }}</label>
                <select
                  id="groupId"
                  class="form-select form-select-lg"
                  formControlName="groupId"
                  [class.is-invalid]="
                    form.controls.groupId.touched && form.controls.groupId.invalid
                  "
                >
                  <option value="" disabled>{{ 'register.groupPlaceholder' | translate }}</option>
                  @for (g of groups(); track g.id) {
                    <option [value]="g.id">{{ g.name }}</option>
                  }
                </select>
                @if (form.controls.groupId.touched && form.controls.groupId.invalid) {
                  <div class="invalid-feedback">{{ 'register.groupRequired' | translate }}</div>
                }
              </div>

              <div class="mb-3">
                <label for="name" class="form-label">{{ 'register.name' | translate }}</label>
                <input
                  id="name"
                  type="text"
                  class="form-control form-control-lg"
                  formControlName="name"
                  autocomplete="name"
                  [class.is-invalid]="form.controls.name.touched && form.controls.name.invalid"
                />
                @if (form.controls.name.touched && form.controls.name.invalid) {
                  <div class="invalid-feedback">{{ 'register.nameRequired' | translate }}</div>
                }
              </div>

              <div class="mb-3">
                <label for="email" class="form-label">{{ 'register.email' | translate }}</label>
                <input
                  id="email"
                  type="email"
                  class="form-control form-control-lg"
                  formControlName="email"
                  autocomplete="username"
                  [class.is-invalid]="form.controls.email.touched && form.controls.email.invalid"
                />
                @if (form.controls.email.touched && form.controls.email.invalid) {
                  <div class="invalid-feedback">{{ 'register.emailInvalid' | translate }}</div>
                }
              </div>

              <div class="mb-3">
                <label for="password" class="form-label">{{
                  'register.password' | translate
                }}</label>
                <input
                  id="password"
                  type="password"
                  class="form-control form-control-lg"
                  formControlName="password"
                  autocomplete="new-password"
                  [class.is-invalid]="
                    form.controls.password.touched && form.controls.password.invalid
                  "
                />
                @if (form.controls.password.touched && form.controls.password.invalid) {
                  <div class="invalid-feedback">{{ 'register.passwordWeak' | translate }}</div>
                }
                <div class="form-text">{{ 'register.passwordHint' | translate }}</div>
              </div>

              <div class="mb-4">
                <label for="confirmPassword" class="form-label">{{
                  'register.confirmPassword' | translate
                }}</label>
                <input
                  id="confirmPassword"
                  type="password"
                  class="form-control form-control-lg"
                  formControlName="confirmPassword"
                  autocomplete="new-password"
                  [class.is-invalid]="
                    form.controls.confirmPassword.touched &&
                    (form.controls.confirmPassword.invalid || form.hasError('passwordMismatch'))
                  "
                />
                @if (form.controls.confirmPassword.touched && form.hasError('passwordMismatch')) {
                  <div class="invalid-feedback d-block">
                    {{ 'register.passwordMismatch' | translate }}
                  </div>
                }
              </div>

              <button type="submit" class="btn btn-primary btn-lg w-100" [disabled]="submitting()">
                @if (submitting()) {
                  <span class="spinner-border spinner-border-sm me-2"></span>
                }
                {{ 'register.submit' | translate }}
              </button>
            </form>

            <p class="text-center small text-muted mt-3 mb-0">
              <a routerLink="/login">{{ 'register.haveAccount' | translate }}</a>
            </p>
          }
        </div>
      </div>
    </div>
  `,
})
export class Register implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly groupsApi = inject(GroupsService);
  protected readonly language = inject(LanguageService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);
  protected readonly groups = signal<PublicGroup[]>([]);

  protected readonly form = this.fb.nonNullable.group(
    {
      groupId: ['', [Validators.required]],
      name: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    this.groupsApi
      .listActive()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((groups) => this.groups.set(groups));
  }

  setLanguage(lang: Lang): void {
    this.language.use(lang);
  }

  submit(): void {
    this.error.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.auth
      .register(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.submitting.set(false);
          this.success.set(response.message);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(httpErrorMessage(err));
        },
      });
  }
}
