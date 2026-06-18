import { HttpErrorResponse } from '@angular/common/http';
import { Component, ChangeDetectionStrategy, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { GroupRole } from '../../core/models/enums';
import { MyGroup } from '../../core/models/models';
import { httpErrorMessage } from '../../core/notifications/http-error';
import { GroupContextService } from '../../core/services/group-context.service';
import { GroupsService } from '../../core/services/groups.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly groupsApi = inject(GroupsService);
  private readonly groupContext = inject(GroupContextService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  submit(): void {
    this.error.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { email, password } = this.form.getRawValue();
    this.auth
      .login(email, password)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.routeByGroups(),
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(httpErrorMessage(err));
        },
      });
  }

  /** After authenticating, pick the group context: 0 -> message, 1 -> auto, many -> chooser. */
  private routeByGroups(): void {
    this.groupContext.clear();
    this.groupsApi
      .myGroups()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (groups) => {
          this.submitting.set(false);
          if (groups.length === 0) {
            this.auth.logout();
            this.error.set(this.translate.instant('group.noApprovedAccess'));
            return;
          }
          if (groups.length === 1) {
            this.enterGroup(groups[0]);
            return;
          }
          this.router.navigate(['/select-group']);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(httpErrorMessage(err));
        },
      });
  }

  private enterGroup(group: MyGroup): void {
    this.groupContext.select(group);
    this.router.navigate([group.role === GroupRole.GroupAdmin ? '/admin' : '/dashboard']);
  }
}
