import { Component, ChangeDetectionStrategy, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { MatchStatus } from '../../../core/models/enums';

/**
 * Live status of a single match. Stays out of the way while the match has not started
 * (the common case), so it can sit in any badge row without adding noise.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-match-status-badge',
  imports: [TranslatePipe],
  template: `
    @if (cls(); as c) {
      <span class="badge {{ c }}">{{ 'matchStatus.' + status() | translate }}</span>
    }
  `,
})
export class MatchStatusBadge {
  readonly status = input<MatchStatus | undefined>();

  /** Empty for NotStarted/absent, which hides the badge. */
  readonly cls = computed(() => {
    switch (this.status()) {
      case MatchStatus.InProgress:
        return 'text-bg-danger';
      case MatchStatus.Finished:
        return 'text-bg-success';
      case MatchStatus.Postponed:
        return 'text-bg-warning';
      case MatchStatus.Cancelled:
        return 'text-bg-secondary';
      default:
        return '';
    }
  });
}
