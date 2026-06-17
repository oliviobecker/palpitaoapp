import { Component, input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  imports: [],
  template: `
    <div class="text-center text-muted py-5">
      <div class="display-6 mb-2">{{ icon() }}</div>
      <p class="mb-2">{{ message() }}</p>
      <ng-content />
    </div>
  `,
})
export class EmptyState {
  readonly icon = input<string>('📭');
  readonly message = input.required<string>();
}
