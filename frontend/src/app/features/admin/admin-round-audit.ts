import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ScoreCategory } from '../../core/models/enums';
import { MatchScore, RoundResultMatch, RoundResults } from '../../core/models/models';
import { RoundsService } from '../../core/services/rounds.service';
import { CompetitionBadge } from '../../shared/components/competition-badge/competition-badge';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { Icon } from '../../shared/components/icon/icon';
import { MultiplierBadge } from '../../shared/components/multiplier-badge/multiplier-badge';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { SkeletonList } from '../../shared/components/skeleton/skeleton-list';
import { phaseLabel } from '../../shared/utils/match.util';

/**
 * Admin audit: every participant's per-match scoring breakdown for a round, so the admin
 * can explain exactly how each total was reached (category, base points, multiplier and
 * the rule context — classic / manual override / phase). Reads the shared results endpoint.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-round-audit',
  imports: [
    TranslatePipe,
    CompetitionBadge,
    EmptyState,
    ErrorState,
    Icon,
    MultiplierBadge,
    PageHeader,
    SkeletonList,
  ],
  template: `
    <app-page-header [trail]="trail()" [title]="'audit.scoringTitle' | translate" />

    @if (loading()) {
      <app-skeleton-list [count]="5" />
    } @else if (error()) {
      <app-error-state (retry)="load()" />
    } @else if (!scored()) {
      <app-empty-state icon="hourglass" [message]="'results.notScored' | translate" />
    } @else {
      @for (p of participants(); track p.userId) {
        <div class="card mb-2">
          <button
            type="button"
            class="card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
            (click)="toggle(p.userId)"
          >
            <span class="fw-semibold d-flex align-items-center gap-2">
              <app-icon [name]="isOpen(p.userId) ? 'chevron-down' : 'chevron-right'" [size]="16" />
              {{ p.name }}
              @if (p.wasAbsent) {
                <span class="badge text-bg-secondary">{{ 'results.absent' | translate }}</span>
              }
              @if (p.flavioRuleApplied) {
                <span class="badge text-bg-warning">{{ 'results.flavioApplied' | translate }}</span>
              }
            </span>
            <span class="h5 fw-bold mb-0">{{ p.finalPoints }}</span>
          </button>

          @if (isOpen(p.userId)) {
            <div class="card-body pt-0 px-3">
              <div class="vstack gap-1">
                @for (m of matches(); track m.roundMatchId) {
                  <div
                    class="d-flex justify-content-between align-items-center small border-top py-1"
                  >
                    <span class="d-flex flex-column">
                      <span class="fw-semibold"
                        >{{ m.homeTeamName }} <span class="text-muted">×</span>
                        {{ m.awayTeamName }}</span
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
                    <span class="text-end">
                      @if (score(p.userId, m.roundMatchId); as s) {
                        @if (categoryKey(s.scoreCategory); as ck) {
                          <span class="text-body d-block">{{ ck | translate }}</span>
                        }
                        <span class="text-muted">{{ s.basePoints }} × {{ s.multiplier }} =</span>
                        <span class="badge text-bg-success ms-1">+{{ s.finalPoints }}</span>
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </span>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    }
  `,
})
export class AdminRoundAudit implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly roundId = signal('');
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly results = signal<RoundResults | null>(null);
  protected readonly expanded = signal<Set<string>>(new Set());

  protected readonly scored = computed(() => (this.results()?.participants.length ?? 0) > 0);
  protected readonly participants = computed(() => this.results()?.participants ?? []);
  protected readonly matches = computed<RoundResultMatch[]>(() => this.results()?.matches ?? []);
  protected readonly trail = computed(() => `Admin · ${this.roundId()}`);
  protected readonly phaseLabel = phaseLabel;

  ngOnInit(): void {
    this.roundId.set(this.route.snapshot.paramMap.get('id') ?? '');
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.roundsApi
      .getResults(this.roundId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (results) => {
          this.results.set(results);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }

  toggle(userId: string): void {
    const next = new Set(this.expanded());
    if (next.has(userId)) {
      next.delete(userId);
    } else {
      next.add(userId);
    }
    this.expanded.set(next);
  }

  isOpen(userId: string): boolean {
    return this.expanded().has(userId);
  }

  score(userId: string, matchId: string): MatchScore | null {
    const p = this.participants().find((x) => x.userId === userId);
    return p?.matchScores.find((s) => s.roundMatchId === matchId) ?? null;
  }

  categoryKey(category?: ScoreCategory): string {
    return category && category !== ScoreCategory.None ? 'category.' + category : '';
  }
}
