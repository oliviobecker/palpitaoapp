import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ScoreCategory } from '../../core/models/enums';
import {
  MatchScore,
  MyPredictions,
  RoundResultMatch,
  RoundResults,
} from '../../core/models/models';
import { RoundsService } from '../../core/services/rounds.service';
import { PredictionsService } from '../../core/services/predictions.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';
import { MultiplierBadge } from '../../shared/components/multiplier-badge/multiplier-badge';

@Component({
  selector: 'app-results',
  imports: [TranslatePipe, RouterLink, CompetitionBadge, EmptyState, Loading, MultiplierBadge],
  templateUrl: './results.html',
})
export class Results implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly predictionsApi = inject(PredictionsService);
  private readonly auth = inject(AuthService);

  protected readonly roundId = signal('');
  protected readonly loading = signal(true);
  protected readonly results = signal<RoundResults | null>(null);
  protected readonly mine = signal<MyPredictions | null>(null);

  protected readonly scored = computed(() => (this.results()?.participants.length ?? 0) > 0);
  protected readonly me = computed(
    () =>
      this.results()?.participants.find((p) => p.userId === this.auth.currentUser()?.id) ?? null,
  );
  protected readonly matches = computed<RoundResultMatch[]>(() => this.results()?.matches ?? []);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.roundId.set(id);
    forkJoin({
      results: this.roundsApi.getResults(id),
      mine: this.predictionsApi.getMine(id),
    }).subscribe({
      next: ({ results, mine }) => {
        this.results.set(results);
        this.mine.set(mine);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  prediction(matchId: string): string {
    const p = this.mine()?.predictions.find((x) => x.roundMatchId === matchId);
    return p ? `${p.predictedHomeScore} - ${p.predictedAwayScore}` : '—';
  }

  score(matchId: string): MatchScore | null {
    return this.me()?.matchScores.find((x) => x.roundMatchId === matchId) ?? null;
  }

  categoryKey(category?: ScoreCategory): string {
    return category && category !== ScoreCategory.None ? 'category.' + category : '';
  }
}
