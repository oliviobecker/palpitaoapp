import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { Icon } from '../icon/icon';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-empty-state',
  imports: [Icon],
  template: `
    <div class="text-center text-muted py-5" role="status">
      <app-icon [name]="icon()" [size]="44" class="mb-2" />
      <p class="mb-2">{{ message() }}</p>
      <ng-content />
    </div>
  `,
})
export class EmptyState {
  /** Lucide icon name shown above the message. */
  readonly icon = input<string>('inbox');
  readonly message = input.required<string>();
}
