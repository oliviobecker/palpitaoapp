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
  template: `
    <div class="vstack gap-2">
      @for (p of round().participants; track p.userId) {
        <div class="card" [class.border-primary]="p.userId === meId()">
          <button
            type="button"
            class="row-toggle card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
            [attr.aria-expanded]="isOpen(p.userId)"
            [attr.aria-controls]="'round-' + p.userId"
            (click)="rowToggle.emit(p.userId)"
          >
            <span class="fw-semibold d-flex align-items-center gap-2 flex-wrap">
              <span class="rank-avatar" [style.background]="avatarColor(p.name)">{{
                initials(p.name)
              }}</span>
              {{ p.name }}
              @if (p.wasAbsent) {
                <span class="badge text-bg-secondary">{{ 'results.absent' | translate }}</span>
              }
              @if (p.flavioRuleApplied) {
                <span class="badge text-bg-warning">{{ 'results.flavioApplied' | translate }}</span>
              }
            </span>
            <span class="d-flex align-items-center gap-2">
              <span class="h6 fw-bold mb-0 calc">{{ p.finalPoints }}</span>
              <app-icon [name]="isOpen(p.userId) ? 'chevron-down' : 'chevron-right'" [size]="16" />
            </span>
          </button>

          @if (isOpen(p.userId)) {
            <div class="card-body pt-0 px-3" [id]="'round-' + p.userId">
              <div class="vstack gap-1">
                @for (m of round().matches; track m.roundMatchId) {
                  <div
                    class="match-row d-flex justify-content-between align-items-center small border-bottom py-1 gap-2"
                    [class.opacity-50]="!hasResult(m)"
                  >
                    <span class="d-flex flex-column">
                      <span class="fw-semibold"
                        >{{ short(m.homeTeamName) }}
                        <span class="calc"
                          >{{ m.homeScore ?? '–' }} × {{ m.awayScore ?? '–' }}</span
                        >
                        {{ short(m.awayTeamName) }}</span
                      >
                      <span class="d-flex align-items-center gap-1 flex-wrap">
                        <app-competition-badge [competition]="m.competition" />
                        <app-multiplier-badge [multiplier]="m.multiplier" />
                        @if (m.isClassic) {
                          <span class="badge text-bg-primary">{{
                            'predictions.classic' | translate
                          }}</span>
                        }
                        @if (m.isManualMultiplier) {
                          <span class="badge text-bg-secondary">{{
                            'results.manualMultiplier' | translate
                          }}</span>
                        }
                        @if (phaseLabel(m.phase); as ph) {
                          <span class="text-muted">{{ ph }}</span>
                        }
                      </span>
                    </span>

                    <span class="text-end flex-shrink-0">
                      @if (score(p, m.roundMatchId); as sc) {
                        <span class="d-block text-muted text-nowrap"
                          >{{ 'publicStandings.prediction' | translate }}
                          <span class="calc text-nowrap"
                            >{{ sc.predictedHomeScore ?? '–' }} ×
                            {{ sc.predictedAwayScore ?? '–' }}</span
                          ></span
                        >
                        <span class="d-block text-body">{{
                          categoryLabelKey(sc.scoreCategory) | translate
                        }}</span>
                        <span class="text-muted calc"
                          >{{ sc.basePoints }} × {{ sc.multiplier }} =</span
                        >
                        <span
                          class="badge ms-1"
                          [class.text-bg-success]="sc.finalPoints > 0"
                          [class.text-bg-secondary]="sc.finalPoints === 0"
                          >+{{ sc.finalPoints }}</span
                        >
                      } @else {
                        <span class="text-muted text-nowrap">{{
                          (hasResult(m)
                            ? 'publicStandings.noPrediction'
                            : 'publicStandings.awaiting'
                          ) | translate
                        }}</span>
                      }
                    </span>
                  </div>
                }
              </div>

              <div class="d-flex flex-wrap gap-3 small text-muted mt-2">
                @if (p.wasAbsent) {
                  <span>{{ 'publicStandings.absentFooter' | translate }}</span>
                }
                @if (p.flavioRuleApplied) {
                  <span
                    >{{ 'publicStandings.gross' | translate }}
                    <span class="calc">{{ p.grossPoints }}</span> ·
                    {{ 'results.flavioApplied' | translate }}
                    <span class="calc">−{{ p.grossPoints - p.finalPoints }}</span></span
                  >
                }
                @if (p.penaltyPoints > 0) {
                  <span class="text-danger"
                    >{{ 'standings.penalties' | translate }}
                    <span class="calc">−{{ p.penaltyPoints }}</span></span
                  >
                }
                <span
                  >{{ 'publicStandings.roundTotal' | translate }}
                  <span class="calc fw-semibold">{{ p.finalPoints }}</span></span
                >
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
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
