import { Component, inject } from '@angular/core';
import { ToastService } from '../../../core/notifications/toast.service';

@Component({
  selector: 'app-toast-container',
  imports: [],
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
              aria-label="Fechar"
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
