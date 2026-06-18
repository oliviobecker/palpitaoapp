import { HttpErrorResponse } from '@angular/common/http';
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
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Mirror as MirrorView, MirrorParticipant } from '../../core/models/models';
import { PredictionsService } from '../../core/services/predictions.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-mirror',
  imports: [FormsModule, RouterLink, TranslatePipe, EmptyState, Loading],
  templateUrl: './mirror.html',
})
export class Mirror implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly predictionsApi = inject(PredictionsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly blocked = signal(false);
  /** True when the API refused access (403) — group has the feature disabled. */
  protected readonly forbidden = signal(false);
  protected readonly mirror = signal<MirrorView | null>(null);
  protected readonly participantFilter = signal('');
  protected readonly matchFilter = signal('');

  protected readonly matchLabels = computed(() => {
    const map = new Map<string, string>();
    for (const m of this.mirror()?.matches ?? []) {
      map.set(m.roundMatchId, `${m.homeTeamName} x ${m.awayTeamName}`);
    }
    return map;
  });

  protected readonly participants = computed<MirrorParticipant[]>(() => {
    const all = this.mirror()?.participants ?? [];
    const filter = this.participantFilter();
    return filter ? all.filter((p) => p.userId === filter) : all;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.predictionsApi
      .getMirror(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (m) => {
          this.mirror.set(m);
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          // 403 = group disabled the feature for participants; otherwise it's the
          // pre-lock "mirror not released yet" case.
          if (err.status === 403) {
            this.forbidden.set(true);
          } else {
            this.blocked.set(true);
          }
          this.loading.set(false);
        },
      });
  }

  matchLabel(roundMatchId: string): string {
    return this.matchLabels().get(roundMatchId) ?? '';
  }

  visiblePredictions(participant: MirrorParticipant) {
    const matchId = this.matchFilter();
    return matchId
      ? participant.predictions.filter((p) => p.roundMatchId === matchId)
      : participant.predictions;
  }
}
