import { Component, ChangeDetectionStrategy, computed, input } from '@angular/core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-multiplier-badge',
  imports: [],
  template: `
    @if (multiplier() > 1) {
      <span class="badge {{ cls() }}">x{{ multiplier() }}</span>
    }
  `,
})
export class MultiplierBadge {
  readonly multiplier = input.required<number>();

  readonly cls = computed(() => {
    const m = this.multiplier();
    if (m >= 3) {
      return 'text-bg-danger';
    }
    if (m === 2) {
      return 'text-bg-warning';
    }
    return 'text-bg-secondary';
  });
}
