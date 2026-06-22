import {
  Component,
  ChangeDetectionStrategy,
  OnDestroy,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Icon } from '../icon/icon';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-countdown',
  imports: [Icon],
  template: `
    @if (target()) {
      <span
        class="badge"
        role="timer"
        [class.text-bg-danger]="urgent()"
        [class.text-bg-secondary]="!urgent()"
        [attr.aria-label]="ariaLabel()"
      >
        <span aria-hidden="true"><app-icon name="timer" [size]="12" /> {{ label() }}</span>
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

  /** Spoken label so screen readers announce what the badge represents. */
  readonly ariaLabel = computed(
    () => `${this.translate.instant('countdown.timeRemaining')}: ${this.label()}`,
  );

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
