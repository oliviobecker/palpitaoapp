import { DatePipe } from '@angular/common';
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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { TemporaryStandings } from '../../core/models/models';
import { RoundsService } from '../../core/services/rounds.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-temporary-standings',
  imports: [RouterLink, TranslatePipe, DatePipe, EmptyState, Loading],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/rounds">{{ 'nav.rounds' | translate }}</a> ·
        {{ 'temporaryStandings.title' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-0">{{ 'temporaryStandings.title' | translate }}</h1>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (data(); as d) {
      <div class="alert alert-warning py-2">⏱️ {{ 'temporaryStandings.notice' | translate }}</div>

      <div class="d-flex flex-wrap gap-3 small text-muted mb-3">
        <span>{{ 'temporaryStandings.computed' | translate }}: {{ d.computedMatches }}</span>
        <span>{{ 'temporaryStandings.remaining' | translate }}: {{ d.remainingMatches }}</span>
        @if (d.lastUpdatedAt) {
          <span
            >{{ 'temporaryStandings.lastUpdated' | translate }}:
            {{ d.lastUpdatedAt | date: 'dd/MM HH:mm' }}</span
          >
        }
      </div>

      @if (d.standings.length === 0) {
        <app-empty-state [message]="'temporaryStandings.empty' | translate" />
      } @else {
        <div class="vstack gap-2">
          @for (s of d.standings; track s.userId) {
            <div class="card" [class.border-primary]="isMe(s.userId)">
              <div class="card-body py-2 px-3">
                <div class="d-flex align-items-center gap-2">
                  <span class="fw-bold fs-5" style="min-width: 1.6rem">{{ s.position }}</span>
                  <div class="flex-grow-1">
                    <div class="fw-semibold">
                      {{ s.name }}
                      @if (isMe(s.userId)) {
                        <span class="badge text-bg-primary ms-1">{{
                          'common.you' | translate
                        }}</span>
                      }
                    </div>
                    <div class="small text-muted">
                      {{ 'temporaryStandings.official' | translate }}:
                      {{ s.currentOfficialTotalPoints }} ·
                      {{ 'temporaryStandings.projected' | translate }}: {{ s.projectedTotalPoints }}
                    </div>
                  </div>
                  <div class="text-end">
                    <div class="fw-bold fs-5 text-primary">+{{ s.roundTemporaryPoints }}</div>
                    <div class="small text-muted">
                      {{ 'temporaryStandings.points' | translate }}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      }
    }
  `,
})
export class TemporaryStandingsView implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roundsApi = inject(RoundsService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly data = signal<TemporaryStandings | null>(null);
  private readonly myId = computed(() => this.auth.currentUser()?.id ?? null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.roundsApi
      .getTemporaryStandings(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (d) => {
          this.data.set(d);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  isMe(userId: string): boolean {
    return this.myId() === userId;
  }
}
