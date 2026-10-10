import { AdminRegistrationRequestsService } from '@core/services/admin/admin-registration-requests.service';
import { AdminUsersService } from '@core/services/admin/admin-users.service';
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
import { RoundStatus } from '@core/models/enums';
import { Participant, RoundSummary, Season } from '@core/models';
import { RoundsService } from '@core/services/rounds.service';
import { SeasonsService } from '@core/services/seasons.service';
import { ErrorState } from '@shared/components/error-state/error-state';
import { Icon } from '@shared/components/icon/icon';
import { PageHeader } from '@shared/components/page-header/page-header';
import { Skeleton } from '@shared/components/skeleton/skeleton';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin',
  imports: [RouterLink, TranslatePipe, ErrorState, PageHeader, Icon, Skeleton, RoundLabelPipe],
  templateUrl: './admin.html',
})
export class Admin implements OnInit {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly roundsApi = inject(RoundsService);
  private readonly registrationsApi = inject(AdminRegistrationRequestsService);
  private readonly usersApi = inject(AdminUsersService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly activeSeason = signal<Season | null>(null);
  protected readonly rounds = signal<RoundSummary[]>([]);
  protected readonly participants = signal<Participant[]>([]);
  protected readonly pendingRequests = signal(0);

  protected readonly openRounds = computed(() =>
    this.rounds().filter((r) => r.status === RoundStatus.Published),
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
      { icon: 'file-pen', tile: 'icon-tile--blue', value: c.draft, label: 'adminDash.drafts' },
      { icon: 'plane', tile: 'icon-tile--teal', value: c.published, label: 'adminDash.published' },
      {
        icon: 'hourglass',
        tile: 'icon-tile--amber',
        value: c.awaiting,
        label: 'adminDash.awaiting',
      },
      {
        icon: 'circle-check',
        tile: 'icon-tile--green',
        value: c.scored,
        label: 'adminDash.scored',
      },
    ];
  });

  protected readonly actionCards: {
    icon: string;
    tile: string;
    title: string;
    sub: string;
    link: string;
    badge?: boolean;
  }[] = [
    {
      icon: 'plus',
      tile: 'icon-tile--green',
      title: 'adminDash.createRound',
      sub: 'adminDash.createRoundSub',
      link: '/admin/rounds/new',
    },
    {
      icon: 'target',
      tile: 'icon-tile--blue',
      title: 'adminDash.registerResult',
      sub: 'adminDash.registerResultSub',
      link: '/admin/rounds',
    },
    {
      icon: 'calculator',
      tile: 'icon-tile--violet',
      title: 'adminDash.scoringRules',
      sub: 'adminDash.scoringRulesSub',
      link: '/admin/scoring',
    },
    {
      icon: 'shield',
      tile: 'icon-tile--teal',
      title: 'adminDash.teams',
      sub: 'adminDash.teamsSub',
      link: '/admin/teams',
    },
    {
      icon: 'tag',
      tile: 'icon-tile--violet',
      title: 'adminDash.aliases',
      sub: 'adminDash.aliasesSub',
      link: '/admin/ocr-aliases',
    },
    {
      icon: 'users',
      tile: 'icon-tile--teal',
      title: 'adminDash.participants',
      sub: 'adminDash.participantsSub',
      link: '/admin/participants',
    },
    {
      icon: 'clipboard-list',
      tile: 'icon-tile--amber',
      title: 'adminDash.registrationRequests',
      sub: 'adminDash.registrationRequestsSub',
      link: '/admin/registration-requests',
      badge: true,
    },
    {
      icon: 'trophy',
      tile: 'icon-tile--green',
      title: 'adminDash.standings',
      sub: 'adminDash.standingsSub',
      link: '/standings',
    },
    {
      icon: 'scroll-text',
      tile: 'icon-tile--blue',
      title: 'adminDash.audit',
      sub: 'adminDash.auditSub',
      link: '/admin/audit',
    },
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    forkJoin({
      season: this.seasonsApi.getActive(),
      rounds: this.roundsApi.getAll(),
      participants: this.usersApi.listParticipants(),
      requests: this.registrationsApi.listRegistrationRequests(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ season, rounds, participants, requests }) => {
          this.activeSeason.set(season);
          this.rounds.set(rounds);
          this.participants.set(participants);
          this.pendingRequests.set(requests.length);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
  }
}
