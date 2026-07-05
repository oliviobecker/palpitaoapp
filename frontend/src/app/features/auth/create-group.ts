import { HttpErrorResponse } from '@angular/common/http';
import { Component, ChangeDetectionStrategy, DestroyRef, inject, signal } from '@angular/core';
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
import { AuthService } from '../../core/auth/auth.service';
import { Lang, LanguageService } from '../../core/i18n/language.service';
import { httpErrorMessage } from '../../core/notifications/http-error';
import { FormField } from '../../shared/components/form-field/form-field';

/** Form-level validator: confirmPassword must equal password. */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-create-group',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, FormField],
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
              <app-form-field
                class="mb-3"
                [label]="'createGroup.groupName' | translate"
                forId="groupName"
                [control]="form.controls.groupName"
                [errors]="{ default: 'createGroup.groupNameRequired' | translate }"
              >
                <input
                  id="groupName"
                  type="text"
                  class="form-control form-control-lg"
                  formControlName="groupName"
                />
              </app-form-field>

              <app-form-field
                class="mb-3"
                [label]="'createGroup.adminName' | translate"
                forId="adminName"
                [control]="form.controls.adminName"
                [errors]="{ default: 'register.nameRequired' | translate }"
              >
                <input
                  id="adminName"
                  type="text"
                  class="form-control form-control-lg"
                  formControlName="adminName"
                  autocomplete="name"
                />
              </app-form-field>

              <app-form-field
                class="mb-3"
                [label]="'register.email' | translate"
                forId="email"
                [control]="form.controls.email"
                [errors]="{ default: 'register.emailInvalid' | translate }"
              >
                <input
                  id="email"
                  type="email"
                  class="form-control form-control-lg"
                  formControlName="email"
                  autocomplete="username"
                />
              </app-form-field>

              <app-form-field
                class="mb-3"
                [label]="'register.password' | translate"
                forId="password"
                [control]="form.controls.password"
                [errors]="{ default: 'register.passwordWeak' | translate }"
                [hint]="'register.passwordHint' | translate"
              >
                <input
                  id="password"
                  type="password"
                  class="form-control form-control-lg"
                  formControlName="password"
                  autocomplete="new-password"
                />
              </app-form-field>

              <app-form-field
                class="mb-4"
                [label]="'register.confirmPassword' | translate"
                forId="confirmPassword"
                [control]="form.controls.confirmPassword"
                [forceError]="
                  form.controls.confirmPassword.touched && form.hasError('passwordMismatch')
                    ? ('register.passwordMismatch' | translate)
                    : ''
                "
              >
                <input
                  id="confirmPassword"
                  type="password"
                  class="form-control form-control-lg"
                  formControlName="confirmPassword"
                  autocomplete="new-password"
                />
              </app-form-field>

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
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

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
    this.auth
      .createGroup(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.submitting.set(false);
          this.success.set(response.message);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(httpErrorMessage(err, this.translate));
        },
      });
  }
}
