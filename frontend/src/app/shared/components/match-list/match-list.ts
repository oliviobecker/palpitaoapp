import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RoundMatch } from '../../../core/models/models';
import { computeMultiplier, isClassic, isLeagueOne } from '../../utils/match.util';
import { CompetitionBadge } from '../competition-badge/competition-badge';
import { Icon } from '../icon/icon';
import { MultiplierBadge } from '../multiplier-badge/multiplier-badge';

/**
 * Presentational list of a round's matches: competition/multiplier badges, classic
 * and League One markers, kickoff time, and the teams. When `editable` is set it
 * shows edit/remove actions and emits the corresponding match.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-match-list',
  imports: [DatePipe, TranslatePipe, CompetitionBadge, Icon, MultiplierBadge],
  template: `
    <div class="vstack gap-2">
      @for (m of matches(); track m.id) {
        <div class="card" [class.border-primary]="classic(m)" [class.border-warning]="leagueOne(m)">
          <div class="card-body py-2 px-3">
            <div class="d-flex flex-wrap gap-2 align-items-center mb-1">
              <app-competition-badge [competition]="m.competition" />
              <app-multiplier-badge [multiplier]="multiplier(m)" />
              @if (classic(m)) {
                <span class="badge text-bg-primary">{{ 'predictions.classic' | translate }}</span>
              }
              @if (leagueOne(m)) {
                <span class="badge text-bg-warning">{{ 'predictions.leagueOne' | translate }}</span>
              }
              <small class="text-muted ms-auto">{{ m.startsAt | date: 'dd/MM HH:mm' }}</small>
            </div>
            <div class="d-flex justify-content-between align-items-center gap-2">
              <span class="fw-semibold"
                >{{ m.homeTeamName }} <span class="text-muted">×</span> {{ m.awayTeamName }}</span
              >
              @if (editable()) {
                <span class="d-flex gap-1" style="flex: none">
                  <button class="btn btn-sm btn-outline-secondary" (click)="edit.emit(m)">
                    <app-icon name="pencil" [size]="14" /> {{ 'common.edit' | translate }}
                  </button>
                  <button class="btn btn-sm btn-outline-danger" (click)="remove.emit(m)">
                    <app-icon name="trash-2" [size]="14" /> {{ 'common.remove' | translate }}
                  </button>
                </span>
              }
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class MatchList {
  readonly matches = input<RoundMatch[]>([]);
  readonly editable = input(false);
  readonly edit = output<RoundMatch>();
  readonly remove = output<RoundMatch>();

  protected readonly multiplier = computeMultiplier;
  protected readonly classic = isClassic;
  protected readonly leagueOne = isLeagueOne;
}
