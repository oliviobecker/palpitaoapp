import { Injectable, signal } from '@angular/core';

/** One checkbox in a confirmation that also asks the user to pick items. */
export interface ConfirmChoice {
  id: string;
  label: string;
  /** Secondary line under the label (e.g. a caveat about the item). */
  hint?: string;
  /** Ticked when the dialog opens. */
  checked?: boolean;
}

/** What a choices confirmation resolves with. */
export interface ConfirmChoicesResult {
  /** Trimmed textarea content, or '' when the dialog had no input. */
  text: string;
  /** Ids of the ticked checkboxes — empty means "confirm, none of them". */
  choiceIds: string[];
}

interface ConfirmState {
  open: boolean;
  title: string;
  message: string;
  confirmText: string;
  danger: boolean;
  withInput: boolean;
  inputLabel: string;
  inputRequired: boolean;
  choicesLabel: string;
  choices: ConfirmChoice[];
  resolve?: (value: boolean) => void;
  resolveInput?: (value: string | null) => void;
  resolveChoices?: (value: ConfirmChoicesResult | null) => void;
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
  choicesLabel: '',
  choices: [],
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

  /**
   * Confirmation with a checkbox list, optionally plus a justification textarea (the two
   * are additive, not exclusive). Resolves with the typed text and the ticked ids, or null
   * when the user cancels — an empty `choiceIds` is a deliberate "confirm, none of them".
   */
  askWithChoices(
    message: string,
    choices: ConfirmChoice[],
    options?: {
      title?: string;
      confirmText?: string;
      danger?: boolean;
      choicesLabel?: string;
      withInput?: boolean;
      inputLabel?: string;
      inputRequired?: boolean;
    },
  ): Promise<ConfirmChoicesResult | null> {
    return new Promise((resolveChoices) => {
      this._state.set({
        ...CLOSED,
        open: true,
        title: options?.title ?? 'Confirmar',
        message,
        confirmText: options?.confirmText ?? 'Confirmar',
        danger: options?.danger ?? false,
        withInput: options?.withInput ?? false,
        inputLabel: options?.inputLabel ?? '',
        inputRequired: options?.inputRequired ?? true,
        choicesLabel: options?.choicesLabel ?? '',
        choices,
        resolveChoices,
      });
    });
  }

  confirm(inputValue?: string, choiceIds?: string[]): void {
    const current = this._state();
    const text = (inputValue ?? '').trim();
    if (current.resolveChoices) {
      current.resolveChoices({ text, choiceIds: choiceIds ?? [] });
    } else if (current.withInput) {
      current.resolveInput?.(text);
    } else {
      current.resolve?.(true);
    }
    this._state.set(CLOSED);
  }

  cancel(): void {
    const current = this._state();
    current.resolve?.(false);
    current.resolveInput?.(null);
    current.resolveChoices?.(null);
    this._state.set(CLOSED);
  }
}
