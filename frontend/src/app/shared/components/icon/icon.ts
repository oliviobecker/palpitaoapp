import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { LucideDynamicIcon } from '@lucide/angular';

/**
 * Thin wrapper over Lucide's dynamic icon so templates use a stable, library-
 * agnostic `<app-icon name="trophy" />`. Icons are registered once via
 * `provideLucideIcons(...)` in app.config.ts (string names are lower-kebab-case).
 *
 * Colour follows `currentColor`, so set the colour with a text class on the host
 * (e.g. `<app-icon name="ban" class="text-danger" />`). Decorative by default
 * (Lucide adds aria-hidden when no title is given).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-icon',
  imports: [LucideDynamicIcon],
  template: `<svg [lucideIcon]="name()" [size]="size()" [strokeWidth]="strokeWidth()"></svg>`,
  styles: [
    `
      :host {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        line-height: 0;
        vertical-align: -0.125em;
      }
    `,
  ],
})
export class Icon {
  readonly name = input.required<string>();
  readonly size = input<string | number>(18);
  readonly strokeWidth = input<string | number>(2);
}
