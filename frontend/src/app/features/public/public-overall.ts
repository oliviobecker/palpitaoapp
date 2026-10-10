import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { PublicStandingRow } from '@core/models';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { Icon } from '@shared/components/icon/icon';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { avatarColor, initials } from '@shared/utils/avatar.util';
import { NumberedRound } from '@shared/utils/round-name.util';
import { RankedRow } from './public-standings.util';

/**
 * The season standings of the public link: podium, name search and one expandable card per
 * participant with their gaps and round history. Presentational — the page owns the state.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-public-overall',
  imports: [TranslatePipe, EmptyState, Icon, RoundLabelPipe],
  styles: `
    :host {
      display: contents;
    }
  `,
  templateUrl: './public-overall.html',
})
export class PublicOverall {
  readonly rows = input.required<RankedRow[]>();
  /** How many rows there are before the name filter. */
  readonly total = input.required<number>();
  readonly podium = input.required<PublicStandingRow[]>();
  readonly meId = input<string | null>(null);
  readonly filter = input('');
  readonly hasRounds = input(false);
  readonly expanded = input.required<ReadonlySet<string>>();

  readonly filterChange = output<string>();
  readonly rowToggle = output<string>();
  /** Open this participant's round breakdown, at a given round or the current one. */
  readonly audit = output<{ userId: string; round?: NumberedRound }>();
  readonly meToggle = output<string>();

  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;

  protected isOpen(userId: string): boolean {
    return this.expanded().has(userId);
  }
}
