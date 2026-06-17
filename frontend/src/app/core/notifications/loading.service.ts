import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly count = signal(0);
  readonly isLoading = computed(() => this.count() > 0);

  start(): void {
    this.count.update((c) => c + 1);
  }

  stop(): void {
    this.count.update((c) => Math.max(0, c - 1));
  }
}
