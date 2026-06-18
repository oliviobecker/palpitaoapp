import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon } from '../icon/icon';

/**
 * Page-level error state: shown when an initial data fetch fails, so the screen
 * gives clear feedback and a way to retry instead of staying blank (the global
 * toast alone disappears in seconds). Mirrors the look of app-empty-state.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-error-state',
  imports: [TranslatePipe, Icon],
  template: `
    <div class="text-center text-muted py-5" role="alert">
      <app-icon name="triangle-alert" [size]="44" class="text-warning mb-2" />
      <p class="mb-3">{{ message() | translate }}</p>
      <button type="button" class="btn btn-outline-primary" (click)="retry.emit()">
        {{ 'common.reload' | translate }}
      </button>
    </div>
  `,
})
export class ErrorState {
  readonly message = input<string>('errors.loadFailed');
  readonly retry = output<void>();
}
