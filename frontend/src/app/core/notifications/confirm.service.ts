import { Injectable, signal } from '@angular/core';

interface ConfirmState {
  open: boolean;
  title: string;
  message: string;
  confirmText: string;
  danger: boolean;
  withInput: boolean;
  inputLabel: string;
  inputRequired: boolean;
  resolve?: (value: boolean) => void;
  resolveInput?: (value: string | null) => void;
}

const CLOSED: ConfirmState = {
  open: false,
  title: '',
  message: '',
  confirmText: 'Confirmar',
  danger: false,
  withInput: false,
  inputLabel: '',
  inputRequired: false,
};

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly _state = signal<ConfirmState>(CLOSED);
  readonly state = this._state.asReadonly();

  ask(
    message: string,
    options?: { title?: string; confirmText?: string; danger?: boolean },
  ): Promise<boolean> {
    return new Promise((resolve) => {
      this._state.set({
        ...CLOSED,
        open: true,
        title: options?.title ?? 'Confirmar',
        message,
        confirmText: options?.confirmText ?? 'Confirmar',
        danger: options?.danger ?? false,
        resolve,
      });
    });
  }

  /**
   * Confirmation that also collects a short text (e.g. a justification).
   * Resolves with the trimmed text, or null when the user cancels.
   */
  askWithInput(
    message: string,
    options?: {
      title?: string;
      confirmText?: string;
      danger?: boolean;
      inputLabel?: string;
      required?: boolean;
    },
  ): Promise<string | null> {
    return new Promise((resolveInput) => {
      this._state.set({
        ...CLOSED,
        open: true,
        title: options?.title ?? 'Confirmar',
        message,
        confirmText: options?.confirmText ?? 'Confirmar',
        danger: options?.danger ?? false,
        withInput: true,
        inputLabel: options?.inputLabel ?? '',
        inputRequired: options?.required ?? true,
        resolveInput,
      });
    });
  }

  confirm(inputValue?: string): void {
    const current = this._state();
    if (current.withInput) {
      current.resolveInput?.((inputValue ?? '').trim());
    } else {
      current.resolve?.(true);
    }
    this._state.set(CLOSED);
  }

  cancel(): void {
    const current = this._state();
    current.resolve?.(false);
    current.resolveInput?.(null);
    this._state.set(CLOSED);
  }
}
