import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { Lang, LanguageService } from '../../core/i18n/language.service';
import { httpErrorMessage } from '../../core/notifications/http-error';

/** Form-level validator: confirmPassword must equal password. */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-create-group',
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
            <p class="text-muted small mb-0">{{ 'createGroup.title' | translate }}</p>
          </div>

          @if (success()) {
            <div class="alert alert-success" role="alert">{{ success() }}</div>
            <a class="btn btn-primary btn-lg w-100" routerLink="/login">{{
              'createGroup.goToLogin' | translate
            }}</a>
          } @else {
            @if (error()) {
              <div class="alert alert-danger py-2" role="alert">{{ error() }}</div>
            }

            <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
              <div class="mb-3">
                <label for="groupName" class="form-label">{{
                  'createGroup.groupName' | translate
                }}</label>
                <input
                  id="groupName"
                  type="text"
                  class="form-control form-control-lg"
                  formControlName="groupName"
                  [class.is-invalid]="
                    form.controls.groupName.touched && form.controls.groupName.invalid
                  "
                />
                @if (form.controls.groupName.touched && form.controls.groupName.invalid) {
                  <div class="invalid-feedback">
                    {{ 'createGroup.groupNameRequired' | translate }}
                  </div>
                }
              </div>

              <div class="mb-3">
                <label for="adminName" class="form-label">{{
                  'createGroup.adminName' | translate
                }}</label>
                <input
                  id="adminName"
                  type="text"
                  class="form-control form-control-lg"
                  formControlName="adminName"
                  autocomplete="name"
                  [class.is-invalid]="
                    form.controls.adminName.touched && form.controls.adminName.invalid
                  "
                />
                @if (form.controls.adminName.touched && form.controls.adminName.invalid) {
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
                {{ 'createGroup.submit' | translate }}
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
export class CreateGroup {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      groupName: ['', [Validators.required, Validators.minLength(2)]],
      adminName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

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
    this.auth.createGroup(this.form.getRawValue()).subscribe({
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
