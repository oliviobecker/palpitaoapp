import { HttpErrorResponse } from '@angular/common/http';
import { Component, ChangeDetectionStrategy, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '@core/auth/auth.service';
import { httpErrorMessage } from '@core/notifications/http-error';
import { FormField } from '@shared/components/form-field/form-field';
import { passwordsMatch, strongPassword } from '@shared/validators/password.validators';
import { LanguageSwitcher } from '@shared/components/language-switcher/language-switcher';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-create-group',
  imports: [LanguageSwitcher, ReactiveFormsModule, RouterLink, TranslatePipe, FormField],
  templateUrl: './create-group.html',
})
export class CreateGroup {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
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
      password: ['', strongPassword],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

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
