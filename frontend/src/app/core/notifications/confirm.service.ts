import { Injectable, signal } from '@angular/core';

interface ConfirmState {
  open: boolean;
  title: string;
  message: string;
  confirmText: string;
  danger: boolean;
  resolve?: (value: boolean) => void;
}

const CLOSED: ConfirmState = {
  open: false,
  title: '',
  message: '',
  confirmText: 'Confirmar',
  danger: false,
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
        open: true,
        title: options?.title ?? 'Confirmar',
        message,
        confirmText: options?.confirmText ?? 'Confirmar',
        danger: options?.danger ?? false,
        resolve,
      });
    });
  }

  confirm(): void {
    this._state().resolve?.(true);
    this._state.set(CLOSED);
  }

  cancel(): void {
    this._state().resolve?.(false);
    this._state.set(CLOSED);
  }
}
