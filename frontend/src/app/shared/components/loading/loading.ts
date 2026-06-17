import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-loading',
  imports: [TranslatePipe],
  template: `
    <div class="text-center py-4">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
      </div>
      <p class="text-muted mt-2 mb-0">{{ message() || ('common.loading' | translate) }}</p>
    </div>
  `,
})
export class Loading {
  readonly message = input<string>('');
}
