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
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { RoundStatus } from '../../core/models/enums';
import { Participant, RoundSummary, Season } from '../../core/models/models';
import { AdminService } from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { SeasonsService } from '../../core/services/seasons.service';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin',
  imports: [RouterLink, TranslatePipe, Loading],
  template: `
    <div class="d-flex justify-content-between align-items-start gap-2 mb-3 flex-wrap">
      <div>
        <div class="page-trail">{{ 'adminDash.panel' | translate }} · Admin</div>
        <h1 class="h4 fw-bold mb-0">{{ 'adminDash.title' | translate }}</h1>
      </div>
      <span class="pill-active">
        {{ 'adminDash.activeSeason' | translate }}:
        {{ activeSeason()?.name ?? ('adminDash.none' | translate) }}
      </span>
    </div>

    @if (loading()) {
      <app-loading />
    } @else {
      <div class="section-label mb-2">{{ 'nav.rounds' | translate }}</div>
      <div class="row g-2 mb-3">
        @for (s of statCards(); track s.label) {
          <div class="col-6 col-lg-3">
            <div class="card stat-card h-100">
              <div class="card-body">
                <span class="icon-tile {{ s.tile }} mb-2">{{ s.icon }}</span>
                <div class="stat-card__value">{{ s.value }}</div>
                <div class="stat-card__label">{{ s.label | translate }}</div>
              </div>
            </div>
          </div>
        }
        <div class="col-6 col-lg-6">
          <div class="card stat-card h-100">
            <div class="card-body">
              <span class="icon-tile icon-tile--green mb-2">👥</span>
              <div class="stat-card__value text-success">{{ activeParticipants() }}</div>
              <div class="stat-card__label">{{ 'adminDash.activeParticipants' | translate }}</div>
            </div>
          </div>
        </div>
        <div class="col-6 col-lg-6">
          <div class="card stat-card h-100">
            <div class="card-body">
              <span class="icon-tile icon-tile--red mb-2">🚫</span>
              <div class="stat-card__value text-danger">{{ eliminatedParticipants() }}</div>
              <div class="stat-card__label">{{ 'adminDash.eliminated' | translate }}</div>
            </div>
          </div>
        </div>
      </div>

      @if (openRound(); as open) {
        <a class="action-card mb-3" routerLink="/admin/rounds/{{ open.id }}">
          <span class="icon-tile icon-tile--green">🟢</span>
          <div>
            <div class="action-card__title">
              {{ 'adminDash.openRound' | translate }}: {{ 'dashboard.round' | translate }}
              {{ open.number }}
            </div>
          </div>
          <span class="action-card__arrow">→</span>
        </a>
      }

      <div class="section-label mb-2">{{ 'adminDash.shortcuts' | translate }}</div>

      <a class="cta-hero mb-3" routerLink="/admin/seasons">
        <span class="cta-hero__icon">📅</span>
        <div>
          <div class="cta-hero__title">{{ 'adminDash.createSeason' | translate }}</div>
          <div class="cta-hero__sub">{{ 'adminDash.createSeasonSub' | translate }}</div>
        </div>
      </a>

      <div class="row g-2">
        @for (a of actionCards; track a.link) {
          <div class="col-12 col-md-6">
            <a class="action-card h-100" [routerLink]="a.link">
              <span class="icon-tile {{ a.tile }}">{{ a.icon }}</span>
              <div>
                <div class="action-card__title">{{ a.title | translate }}</div>
                <div class="action-card__sub">{{ a.sub | translate }}</div>
              </div>
              <span class="action-card__arrow">→</span>
            </a>
          </div>
        }
      </div>
    }
  `,
})
export class Admin implements OnInit {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly roundsApi = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly activeSeason = signal<Season | null>(null);
  protected readonly rounds = signal<RoundSummary[]>([]);
  protected readonly participants = signal<Participant[]>([]);

  protected readonly openRound = computed(
    () => this.rounds().find((r) => r.status === RoundStatus.Published) ?? null,
  );
  protected readonly counts = computed(() => ({
    draft: this.rounds().filter((r) => r.status === RoundStatus.Draft).length,
    published: this.rounds().filter((r) => r.status === RoundStatus.Published).length,
    awaiting: this.rounds().filter((r) => r.status === RoundStatus.Locked).length,
    scored: this.rounds().filter((r) => r.status === RoundStatus.Scored).length,
  }));
  protected readonly activeParticipants = computed(
    () => this.participants().filter((p) => p.isActive && !p.isEliminated).length,
  );
  protected readonly eliminatedParticipants = computed(
    () => this.participants().filter((p) => p.isEliminated).length,
  );

  protected readonly statCards = computed(() => {
    const c = this.counts();
    return [
      { icon: '📝', tile: 'icon-tile--blue', value: c.draft, label: 'adminDash.drafts' },
      { icon: '✈️', tile: 'icon-tile--teal', value: c.published, label: 'adminDash.published' },
      { icon: '⏳', tile: 'icon-tile--amber', value: c.awaiting, label: 'adminDash.awaiting' },
      { icon: '✅', tile: 'icon-tile--green', value: c.scored, label: 'adminDash.scored' },
    ];
  });

  protected readonly actionCards = [
    {
      icon: '➕',
      tile: 'icon-tile--green',
      title: 'adminDash.createRound',
      sub: 'adminDash.createRoundSub',
      link: '/admin/rounds/new',
    },
    {
      icon: '🎯',
      tile: 'icon-tile--blue',
      title: 'adminDash.registerResult',
      sub: 'adminDash.registerResultSub',
      link: '/admin/rounds',
    },
    {
      icon: '👥',
      tile: 'icon-tile--teal',
      title: 'adminDash.participants',
      sub: 'adminDash.participantsSub',
      link: '/admin/participants',
    },
    {
      icon: '📝',
      tile: 'icon-tile--amber',
      title: 'adminDash.registrationRequests',
      sub: 'adminDash.registrationRequestsSub',
      link: '/admin/registration-requests',
    },
    {
      icon: '🏆',
      tile: 'icon-tile--green',
      title: 'adminDash.standings',
      sub: 'adminDash.standingsSub',
      link: '/standings',
    },
    {
      icon: '📜',
      tile: 'icon-tile--blue',
      title: 'adminDash.audit',
      sub: 'adminDash.auditSub',
      link: '/admin/audit',
    },
  ];

  ngOnInit(): void {
    forkJoin({
      season: this.seasonsApi.getActive(),
      rounds: this.roundsApi.getAll(),
      participants: this.adminApi.listParticipants(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ season, rounds, participants }) => {
          this.activeSeason.set(season);
          this.rounds.set(rounds);
          this.participants.set(participants);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
