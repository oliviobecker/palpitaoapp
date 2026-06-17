import { Component, OnDestroy, OnInit, computed, inject, input, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-countdown',
  imports: [],
  template: `
    @if (target()) {
      <span class="badge" [class.text-bg-danger]="urgent()" [class.text-bg-secondary]="!urgent()">
        ⏱ {{ label() }}
      </span>
    }
  `,
})
export class Countdown implements OnInit, OnDestroy {
  readonly target = input<string | Date | null>(null);

  private readonly translate = inject(TranslateService);
  private readonly now = signal(Date.now());
  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  private readonly remainingMs = computed(() => {
    const t = this.target();
    return t ? new Date(t).getTime() - this.now() : 0;
  });

  readonly urgent = computed(() => {
    const ms = this.remainingMs();
    return ms > 0 && ms < 60 * 60 * 1000;
  });

  readonly label = computed(() => {
    const ms = this.remainingMs();
    if (ms <= 0) {
      return this.translate.instant('countdown.ended');
    }
    const total = Math.floor(ms / 1000);
    const days = Math.floor(total / 86400);
    const hours = Math.floor((total % 86400) / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const seconds = total % 60;
    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    }
    if (hours > 0) {
      return `${hours}h ${minutes}m ${seconds}s`;
    }
    return `${minutes}m ${seconds}s`;
  });
}
