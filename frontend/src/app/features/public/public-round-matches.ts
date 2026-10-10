import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { CompetitionBadge } from '@shared/components/competition-badge/competition-badge';
import { Icon } from '@shared/components/icon/icon';
import { MultiplierBadge } from '@shared/components/multiplier-badge/multiplier-badge';
import { avatarColor, initials } from '@shared/utils/avatar.util';
import { phaseLabel } from '@shared/utils/match.util';
import { shortTeamName } from '@shared/utils/team-name.util';
import { MatchPivotRow, categoryLabelKey } from './public-standings.util';

/**
 * A round read match by match — "who got the Arsenal game right?", which is how the argument
 * in the group actually starts: one expandable card per match with everyone's line, best first.
 * Presentational.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-public-round-matches',
  imports: [TranslatePipe, CompetitionBadge, Icon, MultiplierBadge],
  styles: `
    :host {
      display: contents;
    }
  `,
  template: `
    <div class="vstack gap-2">
      @for (row of rows(); track row.match.roundMatchId) {
        <div class="card">
          <button
            type="button"
            class="row-toggle card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
            [attr.aria-expanded]="isOpen(row.match.roundMatchId)"
            [attr.aria-controls]="'match-' + row.match.roundMatchId"
            (click)="matchToggle.emit(row.match.roundMatchId)"
          >
            <span class="d-flex flex-column" style="min-width: 0">
              <span class="fw-semibold"
                >{{ short(row.match.homeTeamName) }}
                <span class="calc"
                  >{{ row.match.homeScore ?? '–' }} × {{ row.match.awayScore ?? '–' }}</span
                >
                {{ short(row.match.awayTeamName) }}</span
              >
              <span class="d-flex align-items-center gap-1 flex-wrap small">
                <app-competition-badge [competition]="row.match.competition" />
                <app-multiplier-badge [multiplier]="row.match.multiplier" />
                @if (row.match.isClassic) {
                  <span class="badge text-bg-primary">{{ 'predictions.classic' | translate }}</span>
                }
                @if (phaseLabel(row.match.phase); as ph) {
                  <span class="text-muted">{{ ph }}</span>
                }
              </span>
            </span>
            <span class="d-flex align-items-center gap-2 flex-shrink-0">
              <span class="small text-muted text-nowrap">{{
                'publicStandings.hits' | translate: { n: row.hits }
              }}</span>
              <app-icon
                [name]="isOpen(row.match.roundMatchId) ? 'chevron-down' : 'chevron-right'"
                [size]="16"
              />
            </span>
          </button>

          @if (isOpen(row.match.roundMatchId)) {
            <div class="card-body pt-0 px-3" [id]="'match-' + row.match.roundMatchId">
              @if (row.entries.length === 0) {
                <div class="small text-muted">
                  {{ 'publicStandings.noPredictionsHere' | translate }}
                </div>
              }
              @for (e of row.entries; track e.userId) {
                <div
                  class="match-row d-flex justify-content-between align-items-center small border-bottom py-1 gap-2"
                >
                  <span
                    class="d-flex align-items-center gap-2"
                    style="min-width: 0"
                    [class.fw-semibold]="e.userId === meId()"
                  >
                    <span class="rank-avatar" [style.background]="avatarColor(e.name)">{{
                      initials(e.name)
                    }}</span>
                    <span class="text-truncate">{{ e.name }}</span>
                  </span>
                  <span class="text-end flex-shrink-0">
                    <span class="d-block text-muted text-nowrap"
                      >{{ 'publicStandings.prediction' | translate }}
                      <span class="calc text-nowrap"
                        >{{ e.score.predictedHomeScore ?? '–' }} ×
                        {{ e.score.predictedAwayScore ?? '–' }}</span
                      ></span
                    >
                    <span class="d-block text-body">{{
                      categoryLabelKey(e.score.scoreCategory) | translate
                    }}</span>
                    <span class="text-muted calc"
                      >{{ e.score.basePoints }} × {{ e.score.multiplier }} =</span
                    >
                    <span
                      class="badge ms-1"
                      [class.text-bg-success]="e.score.finalPoints > 0"
                      [class.text-bg-secondary]="e.score.finalPoints === 0"
                      >+{{ e.score.finalPoints }}</span
                    >
                  </span>
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class PublicRoundMatches {
  readonly rows = input.required<MatchPivotRow[]>();
  readonly meId = input<string | null>(null);
  readonly expanded = input.required<ReadonlySet<string>>();

  readonly matchToggle = output<string>();

  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;
  protected readonly short = shortTeamName;
  protected readonly phaseLabel = phaseLabel;
  protected readonly categoryLabelKey = categoryLabelKey;

  protected isOpen(matchId: string): boolean {
    return this.expanded().has(matchId);
  }
}
