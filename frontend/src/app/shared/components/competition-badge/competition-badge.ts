import { Component, computed, input } from '@angular/core';
import { Competition } from '../../../core/models/enums';

@Component({
  selector: 'app-competition-badge',
  imports: [],
  template: `<span class="badge {{ cls() }}">{{ label() }}</span>`,
})
export class CompetitionBadge {
  readonly competition = input.required<Competition>();

  readonly label = computed(() => {
    switch (this.competition()) {
      case Competition.PremierLeague:
        return 'Premier League';
      case Competition.FACup:
        return 'FA Cup';
      case Competition.Championship:
        return 'Championship';
      case Competition.LeagueOne:
        return 'League One';
      case Competition.FifaWorldCup:
        return 'FIFA World Cup';
      default:
        return this.competition();
    }
  });

  readonly cls = computed(() => {
    switch (this.competition()) {
      case Competition.PremierLeague:
        return 'text-bg-primary';
      case Competition.FACup:
        return 'text-bg-danger';
      case Competition.Championship:
        return 'text-bg-success';
      case Competition.LeagueOne:
        return 'text-bg-warning';
      case Competition.FifaWorldCup:
        return 'text-bg-info';
      default:
        return 'text-bg-secondary';
    }
  });
}
