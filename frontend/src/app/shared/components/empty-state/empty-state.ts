import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-empty-state',
  imports: [],
  template: `
    <div class="text-center text-muted py-5" role="status">
      <div class="display-6 mb-2" aria-hidden="true">{{ icon() }}</div>
      <p class="mb-2">{{ message() }}</p>
      <ng-content />
    </div>
  `,
})
export class EmptyState {
  readonly icon = input<string>('📭');
  readonly message = input.required<string>();
}
