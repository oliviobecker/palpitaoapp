import {
  Component,
  ChangeDetectionStrategy,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ConfirmService } from '../../../core/notifications/confirm.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-confirm-dialog',
  imports: [TranslatePipe],
  template: `
    @if (confirm.state().open) {
      <div
        class="modal d-block"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        style="background: rgba(0,0,0,.5)"
        (keydown)="onKeydown($event)"
      >
        <div #dialog class="modal-dialog modal-dialog-centered">
          <div class="modal-content">
            <div class="modal-header">
              <h2 id="confirm-dialog-title" class="modal-title h6 mb-0">
                {{ confirm.state().title }}
              </h2>
            </div>
            <div class="modal-body">
              <p class="mb-0">{{ confirm.state().message }}</p>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" (click)="confirm.cancel()">
                {{ 'confirm.no' | translate }}
              </button>
              <button
                #confirmBtn
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

  private readonly dialog = viewChild<ElementRef<HTMLElement>>('dialog');
  private readonly confirmBtn = viewChild<ElementRef<HTMLButtonElement>>('confirmBtn');

  /** Element focused before the dialog opened, restored when it closes. */
  private lastFocused: HTMLElement | null = null;

  constructor() {
    // Move focus into the dialog when it opens and restore it when it closes.
    effect(() => {
      const open = this.confirm.state().open;
      const btn = this.confirmBtn();
      if (open && btn) {
        if (!this.lastFocused) {
          this.lastFocused = document.activeElement as HTMLElement | null;
        }
        btn.nativeElement.focus();
      } else if (!open && this.lastFocused) {
        this.lastFocused.focus();
        this.lastFocused = null;
      }
    });
  }

  /** ESC closes the dialog; Tab is trapped within its focusable elements. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.confirm.cancel();
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }
    const focusables = this.dialog()?.nativeElement.querySelectorAll<HTMLElement>('button');
    if (!focusables || focusables.length === 0) {
      return;
    }
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
