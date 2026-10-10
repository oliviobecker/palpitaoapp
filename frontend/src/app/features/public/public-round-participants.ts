import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { PublicRound } from '@core/models';
import { CompetitionBadge } from '@shared/components/competition-badge/competition-badge';
import { Icon } from '@shared/components/icon/icon';
import { MultiplierBadge } from '@shared/components/multiplier-badge/multiplier-badge';
import { avatarColor, initials } from '@shared/utils/avatar.util';
import { phaseLabel } from '@shared/utils/match.util';
import { shortTeamName } from '@shared/utils/team-name.util';
import { categoryLabelKey, hasResult, matchScore } from './public-standings.util';

/**
 * A round read participant by participant — "how did I do?": one expandable card each, with
 * every match's prediction and how its points were earned. Presentational.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-public-round-participants',
  imports: [TranslatePipe, CompetitionBadge, Icon, MultiplierBadge],
  styles: `
    :host {
      display: contents;
    }
  `,
  templateUrl: './public-round-participants.html',
})
export class PublicRoundParticipants {
  readonly round = input.required<PublicRound>();
  readonly meId = input<string | null>(null);
  readonly expanded = input.required<ReadonlySet<string>>();

  readonly rowToggle = output<string>();

  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;
  protected readonly short = shortTeamName;
  protected readonly phaseLabel = phaseLabel;
  protected readonly score = matchScore;
  protected readonly hasResult = hasResult;
  protected readonly categoryLabelKey = categoryLabelKey;

  protected isOpen(userId: string): boolean {
    return this.expanded().has(userId);
  }
}
