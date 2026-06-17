import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ConfirmService } from '../../../core/notifications/confirm.service';

@Component({
  selector: 'app-confirm-dialog',
  imports: [TranslatePipe],
  template: `
    @if (confirm.state().open) {
      <div class="modal d-block" tabindex="-1" role="dialog" style="background: rgba(0,0,0,.5)">
        <div class="modal-dialog modal-dialog-centered">
          <div class="modal-content">
            <div class="modal-header">
              <h2 class="modal-title h6 mb-0">{{ confirm.state().title }}</h2>
            </div>
            <div class="modal-body">
              <p class="mb-0">{{ confirm.state().message }}</p>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" (click)="confirm.cancel()">
                {{ 'confirm.no' | translate }}
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-danger]="confirm.state().danger"
                [class.btn-primary]="!confirm.state().danger"
                (click)="confirm.confirm()"
              >
                {{ confirm.state().confirmText }}
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialog {
  protected readonly confirm = inject(ConfirmService);
}
