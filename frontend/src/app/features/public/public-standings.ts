import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '@core/i18n/language.service';
import { PublicRound, PublicRoundSummary, PublicSeason, PublicStandingRow } from '@core/models';
import { PublicStandingsService } from '@core/services/public-standings.service';
import { ThemeService } from '@core/theme/theme.service';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { ErrorState } from '@shared/components/error-state/error-state';
import { Icon } from '@shared/components/icon/icon';
import { SkeletonList } from '@shared/components/skeleton/skeleton-list';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import {
  NumberedRound,
  parseRoundLabel,
  roundLabel,
  sameRound,
} from '@shared/utils/round-name.util';
import { LanguageSwitcher } from '@shared/components/language-switcher/language-switcher';
import { PublicOverall } from './public-overall';
import { PublicRoundMatches } from './public-round-matches';
import { PublicRoundParticipants } from './public-round-participants';
import { formatRoundDates, pivotByMatch, rankRows, resolveRound } from './public-standings.util';

type Tab = 'overall' | 'round';
type Cut = 'participant' | 'match';

/**
 * Public, read-only standings and scoring audit, reached by a season's public key and no
 * account at all. The audit is not a separate screen: a participant's row expands in
 * place, so the reader never loses sight of who is around them while checking a number.
 *
 * Lives outside the Shell — no navbar, no group context, no session.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-public-standings',
  imports: [
    LanguageSwitcher,
    TranslatePipe,
    EmptyState,
    ErrorState,
    Icon,
    SkeletonList,
    RoundLabelPipe,
    PublicOverall,
    PublicRoundParticipants,
    PublicRoundMatches,
  ],
  template: `
    <div class="public-wrap">
      <header class="d-flex justify-content-between align-items-start gap-2 mb-3">
        <div>
          <div class="d-flex align-items-center gap-2">
            <span aria-hidden="true">⚽</span>
            <span class="fw-bold">{{ season()?.groupName || ('app.name' | translate) }}</span>
          </div>
          @if (season(); as s) {
            <div class="small text-muted">
              {{ s.seasonName }} · {{ 'publicStandings.readOnly' | translate }}
            </div>
          }
        </div>
        <div class="d-flex gap-1">
          <app-language-switcher />
          <button
            type="button"
            class="btn btn-sm btn-outline-secondary"
            (click)="theme.toggle()"
            [attr.aria-label]="'theme.toggle' | translate"
          >
            <app-icon [name]="theme.current() === 'dark' ? 'sun' : 'moon'" [size]="16" />
          </button>
        </div>
      </header>

      @if (loading()) {
        <app-skeleton-list [count]="5" />
      } @else if (invalidKey()) {
        <app-empty-state icon="triangle-alert" [message]="'publicStandings.notFound' | translate" />
      } @else if (error()) {
        <app-error-state (retry)="load()" />
      } @else if (season(); as s) {
        <div class="btn-group w-100 mb-3" role="group">
          <button
            type="button"
            class="btn btn-outline-primary"
            [class.active]="tab() === 'overall'"
            (click)="selectTab('overall')"
          >
            {{ 'publicStandings.overall' | translate }}
          </button>
          <button
            type="button"
            class="btn btn-outline-primary"
            [class.active]="tab() === 'round'"
            [disabled]="s.rounds.length === 0"
            (click)="selectTab('round')"
          >
            {{ 'publicStandings.byRound' | translate }}
          </button>
        </div>

        @if (tab() === 'round') {
          @if (missingRound(); as wanted) {
            <div class="alert alert-secondary py-2 small" role="status">
              {{ 'publicStandings.roundUnavailable' | translate: { n: wanted } }}
            </div>
          }
          <div class="mb-3">
            <label class="form-label small" for="round-select">{{
              'publicStandings.round' | translate
            }}</label>
            <!-- [selected] per option: a [value] on the select lands before the options exist,
                 so a deep-linked round showed the newest one in the picker. -->
            <select
              id="round-select"
              class="form-select"
              (change)="pickRound($any($event.target).value)"
            >
              <!-- Keyed by label: the parts of a round played in parts share a number. -->
              @for (r of s.rounds; track r.number + '.' + (r.part ?? 0)) {
                <option [value]="r.number | roundLabel: r.part" [selected]="isSelected(r)">
                  {{ 'publicStandings.roundN' | translate: { n: (r.number | roundLabel: r.part) } }}
                  @if (r.title) {
                    · {{ r.title }}
                  }
                  @if (roundDates(r); as dates) {
                    · {{ dates }}
                  }
                  @if (!r.isScored) {
                    · {{ 'publicStandings.partial' | translate }}
                  }
                </option>
              }
            </select>
          </div>
        }

        @if (tab() === 'overall') {
          @if (standingsError()) {
            <app-error-state (retry)="loadStandings()" />
          } @else if (standings().length === 0) {
            <app-empty-state icon="trophy" [message]="'publicStandings.noStandings' | translate" />
          } @else {
            <app-public-overall
              [rows]="rows()"
              [total]="standings().length"
              [podium]="podium()"
              [meId]="meId()"
              [filter]="filter()"
              [hasRounds]="s.rounds.length > 0"
              [expanded]="expanded()"
              (filterChange)="filter.set($event)"
              (rowToggle)="toggle($event)"
              (audit)="auditParticipant($event.userId, $event.round)"
              (meToggle)="markMe($event)"
            />
          }
        } @else {
          @if (roundLoading()) {
            <app-skeleton-list [count]="4" />
          } @else if (roundError()) {
            <app-error-state (retry)="reloadRound()" />
          } @else if (round(); as r) {
            @if (r.isPartial) {
              <div class="alert alert-warning py-2 small d-flex align-items-center gap-2">
                <app-icon name="timer" [size]="16" />
                <span>
                  {{ 'publicStandings.partialNotice' | translate }}
                  ({{
                    'publicStandings.computedOf'
                      | translate: { done: r.computedMatches, left: r.remainingMatches }
                  }})
                </span>
              </div>
            }

            @if (r.participants.length === 0) {
              <app-empty-state
                icon="hourglass"
                [message]="'publicStandings.noScores' | translate"
              />
            } @else {
              <!-- Two ways to read the same round. "By participant" answers "how did I do?";
                   "by match" answers "who got the Arsenal game right?", which is how the
                   argument in the group actually starts. -->
              <div class="d-flex justify-content-between align-items-center gap-2 mb-2 flex-wrap">
                <div class="btn-group btn-group-sm" role="group">
                  <button
                    type="button"
                    class="btn btn-outline-secondary"
                    [class.active]="cut() === 'participant'"
                    (click)="cut.set('participant')"
                  >
                    {{ 'publicStandings.byParticipant' | translate }}
                  </button>
                  <button
                    type="button"
                    class="btn btn-outline-secondary"
                    [class.active]="cut() === 'match'"
                    (click)="cut.set('match')"
                  >
                    {{ 'publicStandings.byMatch' | translate }}
                  </button>
                </div>
                <button
                  type="button"
                  class="btn btn-sm btn-link text-decoration-none p-0"
                  (click)="toggleAll(r)"
                >
                  {{
                    (allOpen(r) ? 'publicStandings.collapseAll' : 'publicStandings.expandAll')
                      | translate
                  }}
                </button>
              </div>

              @if (cut() === 'participant') {
                <app-public-round-participants
                  [round]="r"
                  [meId]="meId()"
                  [expanded]="expanded()"
                  (rowToggle)="toggle($event)"
                />
              } @else {
                <app-public-round-matches
                  [rows]="byMatch()"
                  [meId]="meId()"
                  [expanded]="expandedMatches()"
                  (matchToggle)="toggleMatch($event)"
                />
              }
            }
          }
        }

        <details class="mt-4">
          <summary class="small text-muted">{{ 'publicStandings.howItWorks' | translate }}</summary>
          <div class="small mt-2">
            <div class="row g-1 calc">
              <div class="col-6">
                {{ 'category.ColumnOnly' | translate }}: {{ s.ruleset.columnOnly }}
              </div>
              <div class="col-6">
                {{ 'category.Traditional' | translate }}: {{ s.ruleset.traditional }}
              </div>
              <div class="col-6">{{ 'category.Medium' | translate }}: {{ s.ruleset.medium }}</div>
              <div class="col-6">
                {{ 'category.Uncommon' | translate }}: {{ s.ruleset.uncommon }}
              </div>
              <div class="col-6">
                {{ 'category.ExtraUncommon' | translate }}: {{ s.ruleset.extraUncommon }}
              </div>
              <div class="col-6">{{ 'publicStandings.missed' | translate }}: 0</div>
            </div>
            <div class="text-muted mt-2">{{ 'publicStandings.multiplierNote' | translate }}</div>
          </div>
        </details>

        <footer class="text-center small text-muted mt-4 pt-3 border-top">
          <a class="text-muted" href="/">{{ 'publicStandings.about' | translate }}</a>
        </footer>
      }
    </div>
  `,
})
export class PublicStandings implements OnInit {
  private readonly api = inject(PublicStandingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly meta = inject(Meta);

  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);

  protected readonly key = signal('');
  protected readonly loading = signal(true);
  protected readonly roundLoading = signal(false);
  protected readonly error = signal(false);
  protected readonly invalidKey = signal(false);
  protected readonly standingsError = signal(false);
  protected readonly roundError = signal(false);
  /** Set, as a label, when a deep link asked for a round the season does not publish. */
  protected readonly missingRound = signal<string | null>(null);
  protected readonly season = signal<PublicSeason | null>(null);
  protected readonly standings = signal<PublicStandingRow[]>([]);
  protected readonly round = signal<PublicRound | null>(null);
  protected readonly tab = signal<Tab>('overall');
  /** The round on screen: number plus part, since a round played in parts shares its number. */
  protected readonly selectedRound = signal<NumberedRound | null>(null);
  protected readonly expanded = signal<Set<string>>(new Set());

  /** Free-text search over the standings. Filters, never reorders. */
  protected readonly filter = signal('');

  /**
   * Which row the reader claims as their own. There is no session here, so this is a local
   * choice: stored per key, on this device only, and never sent anywhere.
   */
  protected readonly meId = signal<string | null>(null);

  /** How the round is sliced: one card per participant, or one per match. */
  protected readonly cut = signal<Cut>('participant');
  protected readonly expandedMatches = signal<Set<string>>(new Set());

  /** Rounds newest-first, as the API returns them. */
  protected readonly rounds = computed(() => this.season()?.rounds ?? []);

  /** The selected round as it reads in the URL: "10", or "10.2" for a part. */
  private readonly selectedLabel = computed(() => {
    const selected = this.selectedRound();
    return selected ? roundLabel(selected.number, selected.part) : null;
  });

  /** Top three, for the podium — same treatment as the in-app standings. */
  protected readonly podium = computed(() => this.standings().slice(0, 3));

  /** The standings with each row's gaps, filtered by name (see rankRows). */
  protected readonly rows = computed(() => rankRows(this.standings(), this.filter()));

  ngOnInit(): void {
    // Keeps the page out of search results. robots.txt stops the crawl; this tag is what
    // de-indexes a URL that was already discovered (e.g. pasted in a public thread). It is
    // removed on leave so the rest of the app is unaffected.
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    this.destroyRef.onDestroy(() => this.meta.removeTag("name='robots'"));

    // The key may arrive in the path (/p/:key) or the query string (/p?key=…).
    const params = this.route.snapshot;
    const key = params.paramMap.get('key') ?? params.queryParamMap.get('key') ?? '';
    this.key.set(key);

    // "?rodada=10" or, for a part of a round played in parts, "?rodada=10.2".
    const round = parseRoundLabel(params.queryParamMap.get('rodada'));
    if (round) {
      this.selectedRound.set(round);
      this.tab.set('round');
    }
    const participant = params.queryParamMap.get('participante');
    if (participant) {
      this.expanded.set(new Set(participant.split(',').filter(Boolean)));
    }

    this.meId.set(this.readMe());
    this.load();
  }

  load(): void {
    if (!this.key()) {
      this.invalidKey.set(true);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(false);
    this.invalidKey.set(false);

    this.api
      .season(this.key())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (season) => {
          this.season.set(season);
          this.loading.set(false);
          this.loadStandings();
          if (this.tab() === 'round') {
            this.ensureRound();
          }
        },
        error: (err: { status?: number }) => {
          // A bad or unpublished key is the expected case for a stale link, not a fault:
          // it gets its own message instead of the generic retry-me error state.
          if (err?.status === 404) {
            this.invalidKey.set(true);
          } else {
            this.error.set(true);
          }
          this.loading.set(false);
        },
      });
  }

  loadStandings(): void {
    this.standingsError.set(false);
    this.api
      .standings(this.key())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rows) => this.standings.set(rows),
        // Without this, a failed load looks exactly like a season with no points yet.
        error: () => this.standingsError.set(true),
      });
  }

  private ensureRound(): void {
    const available = this.rounds();
    if (available.length === 0) {
      return;
    }
    const { round: selected, missing } = resolveRound(available, this.selectedRound());
    this.missingRound.set(missing);
    this.selectedRound.set(selected);

    // Switching tabs back and forth should not re-fetch a round already in hand — it
    // flashes the skeleton and burns the public endpoint's per-IP quota.
    const current = this.round();
    if (!current || !sameRound(current, selected) || this.roundError()) {
      this.loadRound(selected);
    }
  }

  private loadRound(target: NumberedRound): void {
    this.roundLoading.set(true);
    this.roundError.set(false);
    this.api
      .round(this.key(), target.number, target.part)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.round.set(r);
          this.roundLoading.set(false);
        },
        error: () => {
          this.round.set(null);
          this.roundError.set(true);
          this.roundLoading.set(false);
        },
      });
  }

  /** Retry hook for the round error state. */
  reloadRound(): void {
    const selected = this.selectedRound();
    if (selected !== null) {
      this.loadRound(selected);
    }
  }

  protected isSelected(r: NumberedRound): boolean {
    const selected = this.selectedRound();
    return selected !== null && sameRound(r, selected);
  }

  selectTab(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'round') {
      this.ensureRound();
    }
    this.syncUrl();
  }

  /** `value` is the option's label ("10" or "10.2"), never parsed as a decimal number. */
  pickRound(value: string): void {
    const picked = parseRoundLabel(value);
    if (!picked) {
      return;
    }
    this.selectedRound.set(picked);
    this.missingRound.set(null);
    this.loadRound(picked);
    this.syncUrl();
  }

  /**
   * Jumps from the overall tab into this participant's round breakdown, optionally at the
   * round the reader just pointed at in the history strip.
   */
  auditParticipant(userId: string, round?: NumberedRound): void {
    this.expanded.set(new Set([userId]));
    this.cut.set('participant');
    this.tab.set('round');
    if (round != null) {
      this.selectedRound.set({ number: round.number, part: round.part ?? 0 });
    }
    this.ensureRound();
    this.syncUrl();
    // The button that was just pressed no longer exists; without this the focus ring
    // falls back to the document and keyboard readers lose their place.
    setTimeout(() => document.getElementById('round-select')?.focus());
  }

  toggle(userId: string): void {
    const next = new Set(this.expanded());
    if (next.has(userId)) {
      next.delete(userId);
    } else {
      next.add(userId);
    }
    this.expanded.set(next);
    this.syncUrl();
  }

  isOpen(userId: string): boolean {
    return this.expanded().has(userId);
  }

  /**
   * Marks (or unmarks) a row as the reader's own. Pressing it again clears the mark, so a
   * shared phone is never stuck claiming to be someone else.
   */
  markMe(userId: string): void {
    const next = this.meId() === userId ? null : userId;
    this.meId.set(next);
    try {
      if (next) {
        localStorage.setItem(this.meStorageKey(), next);
      } else {
        localStorage.removeItem(this.meStorageKey());
      }
    } catch {
      // Private mode or storage disabled: the highlight simply does not survive the visit.
    }
  }

  private meStorageKey(): string {
    return `palpitao.publicMe.${this.key().toUpperCase()}`;
  }

  private readMe(): string | null {
    try {
      return localStorage.getItem(this.meStorageKey());
    } catch {
      return null;
    }
  }

  /** The round transposed: one row per match with everyone's line, best first. */
  protected readonly byMatch = computed(() => pivotByMatch(this.round()));

  isMatchOpen(matchId: string): boolean {
    return this.expandedMatches().has(matchId);
  }

  toggleMatch(matchId: string): void {
    const next = new Set(this.expandedMatches());
    if (next.has(matchId)) {
      next.delete(matchId);
    } else {
      next.add(matchId);
    }
    this.expandedMatches.set(next);
  }

  /** True when every card of the active cut is already open. */
  allOpen(round: PublicRound): boolean {
    return this.cut() === 'participant'
      ? round.participants.length > 0 && round.participants.every((p) => this.isOpen(p.userId))
      : round.matches.length > 0 && round.matches.every((m) => this.isMatchOpen(m.roundMatchId));
  }

  /**
   * Opens or closes the whole round at once — the one action that makes a full-round
   * screenshot possible, which is what ends up back in the group chat.
   */
  toggleAll(round: PublicRound): void {
    const open = this.allOpen(round);
    if (this.cut() === 'participant') {
      this.expanded.set(open ? new Set() : new Set(round.participants.map((p) => p.userId)));
      this.syncUrl();
    } else {
      this.expandedMatches.set(
        open ? new Set() : new Set(round.matches.map((m) => m.roundMatchId)),
      );
    }
  }

  protected roundDates(round: PublicRoundSummary): string {
    return formatRoundDates(round, this.language.current());
  }

  /**
   * Mirrors the current view into the URL so a reader can share the exact slice they are
   * looking at. Replaces history instead of stacking it — expanding three rows should not
   * mean pressing Back three times.
   */
  private syncUrl(): void {
    const open = [...this.expanded()];
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        key: this.route.snapshot.paramMap.get('key') ? null : this.key(),
        rodada: this.tab() === 'round' ? this.selectedLabel() : null,
        participante: open.length > 0 ? open.join(',') : null,
      },
      queryParamsHandling: 'merge',
    });
  }
}
