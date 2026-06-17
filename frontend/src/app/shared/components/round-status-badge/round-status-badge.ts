import { Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RoundStatus } from '../../../core/models/enums';

@Component({
  selector: 'app-round-status-badge',
  imports: [TranslatePipe],
  template: `<span class="badge {{ cls() }}">{{ 'status.' + status() | translate }}</span>`,
})
export class RoundStatusBadge {
  readonly status = input.required<RoundStatus>();

  readonly cls = computed(() => {
    switch (this.status()) {
      case RoundStatus.Draft:
        return 'text-bg-secondary';
      case RoundStatus.Published:
        return 'text-bg-success';
      case RoundStatus.Locked:
        return 'text-bg-warning';
      case RoundStatus.Scored:
        return 'text-bg-primary';
      case RoundStatus.Cancelled:
        return 'text-bg-danger';
      default:
        return 'text-bg-secondary';
    }
  });
}
