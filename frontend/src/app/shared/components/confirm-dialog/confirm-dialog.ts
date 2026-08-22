import {
  Component,
  ChangeDetectionStrategy,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
  viewChildren,
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
              @if (confirm.state().choices.length) {
                <fieldset class="mt-3">
                  <legend class="form-label fs-6 mb-2">{{ confirm.state().choicesLabel }}</legend>
                  <div class="vstack gap-2">
                    @for (choice of confirm.state().choices; track choice.id) {
                      <div class="form-check">
                        <input
                          #choiceEl
                          type="checkbox"
                          class="form-check-input"
                          [id]="'confirm-dialog-choice-' + choice.id"
                          [checked]="checked().has(choice.id)"
                          (change)="toggleChoice(choice.id)"
                        />
                        <label
                          class="form-check-label"
                          [for]="'confirm-dialog-choice-' + choice.id"
                        >
                          {{ choice.label }}
                          @if (choice.hint) {
                            <small class="d-block text-warning-emphasis">{{ choice.hint }}</small>
                          }
                        </label>
                      </div>
                    }
                  </div>
                </fieldset>
              }
              @if (confirm.state().withInput) {
                <div class="mt-3">
                  <label for="confirm-dialog-input" class="form-label">
                    {{ confirm.state().inputLabel }}
                  </label>
                  <textarea
                    #inputEl
                    id="confirm-dialog-input"
                    class="form-control"
                    rows="2"
                    [value]="inputValue()"
                    (input)="inputValue.set($any($event.target).value)"
                  ></textarea>
                </div>
              }
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
                [disabled]="!canConfirm()"
                (click)="confirm.confirm(inputValue(), [...checked()])"
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
  private readonly inputEl = viewChild<ElementRef<HTMLTextAreaElement>>('inputEl');
  private readonly choiceEls = viewChildren<ElementRef<HTMLInputElement>>('choiceEl');

  protected readonly inputValue = signal('');
  protected readonly checked = signal<ReadonlySet<string>>(new Set());

  protected toggleChoice(id: string): void {
    const next = new Set(this.checked());
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    this.checked.set(next);
  }

  protected readonly canConfirm = computed(() => {
    const state = this.confirm.state();
    return !state.withInput || !state.inputRequired || this.inputValue().trim().length > 0;
  });

  /** Element focused before the dialog opened, restored when it closes. */
  private lastFocused: HTMLElement | null = null;

  constructor() {
    // Move focus into the dialog when it opens and restore it when it closes.
    effect(() => {
      const state = this.confirm.state();
      const btn = this.confirmBtn();
      const input = this.inputEl();
      if (state.open) {
        if (!this.lastFocused) {
          this.lastFocused = document.activeElement as HTMLElement | null;
          this.inputValue.set('');
          this.checked.set(new Set(state.choices.filter((c) => c.checked).map((c) => c.id)));
        }
        // Land on the first thing the user has to fill in, never past it.
        if (state.withInput && input) {
          input.nativeElement.focus();
        } else if (this.choiceEls().length) {
          this.choiceEls()[0].nativeElement.focus();
        } else if (btn) {
          btn.nativeElement.focus();
        }
      } else if (this.lastFocused) {
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
    const focusables = Array.from(
      this.dialog()?.nativeElement.querySelectorAll<HTMLElement>(
        'button, textarea, input, select, a[href], [tabindex]:not([tabindex="-1"])',
      ) ?? [],
    ).filter((el) => !(el as HTMLButtonElement | HTMLInputElement).disabled);
    if (focusables.length === 0) {
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
