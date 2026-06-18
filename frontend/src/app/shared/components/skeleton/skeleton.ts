import { Component, ChangeDetectionStrategy, input } from '@angular/core';

/**
 * A single shimmering placeholder block. Compose several to mirror the shape of
 * the content that is loading, so screens reveal their layout instead of a bare
 * spinner. Purely decorative (aria-hidden) — the shell's top loading bar already
 * signals activity to assistive tech.
 *
 * The shimmer animation and base look live in the global `.skeleton` rule
 * (styles.scss), which also disables the animation under prefers-reduced-motion.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-skeleton',
  imports: [],
  template: `<span
    class="skeleton"
    aria-hidden="true"
    [class.skeleton--circle]="circle()"
    [style.width]="width()"
    [style.height]="height()"
    [style.border-radius]="circle() ? '50%' : radius()"
  ></span>`,
})
export class Skeleton {
  readonly width = input<string>('100%');
  readonly height = input<string>('1rem');
  /** Border radius when not a circle. Defaults to the small token radius. */
  readonly radius = input<string>('var(--radius-sm)');
  readonly circle = input<boolean>(false);
}
