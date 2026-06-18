import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../core/notifications/toast.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-toast-container',
  imports: [TranslatePipe],
  template: `
    <div class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 1100">
      @for (toast of toasts.toasts(); track toast.id) {
        <div
          class="toast show align-items-center text-white {{ bg(toast.type) }} border-0 mb-2"
          role="alert"
        >
          <div class="d-flex">
            <div class="toast-body">{{ toast.text }}</div>
            <button
              type="button"
              class="btn-close btn-close-white me-2 m-auto"
              [attr.aria-label]="'common.close' | translate"
              (click)="toasts.dismiss(toast.id)"
            ></button>
          </div>
        </div>
      }
    </div>
  `,
})
export class ToastContainer {
  protected readonly toasts = inject(ToastService);

  protected bg(type: string): string {
    switch (type) {
      case 'success':
        return 'bg-success';
      case 'error':
        return 'bg-danger';
      default:
        return 'bg-secondary';
    }
  }
}
