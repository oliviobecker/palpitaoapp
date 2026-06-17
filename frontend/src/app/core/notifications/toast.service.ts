import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  text: string;
  type: ToastType;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _toasts = signal<Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();
  private seq = 0;

  success(text: string): void {
    this.show(text, 'success');
  }

  error(text: string): void {
    this.show(text, 'error', 7000);
  }

  info(text: string): void {
    this.show(text, 'info');
  }

  dismiss(id: number): void {
    this._toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(text: string, type: ToastType, timeout = 4000): void {
    const id = ++this.seq;
    this._toasts.update((list) => [...list, { id, text, type }]);
    setTimeout(() => this.dismiss(id), timeout);
  }
}
