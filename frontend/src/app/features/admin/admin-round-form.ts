import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { FixtureCandidate, RoundSummary, Season } from '../../core/models/models';
import { ToastService } from '../../core/notifications/toast.service';
import { AdminService } from '../../core/services/admin.service';
import { RoundsService } from '../../core/services/rounds.service';
import { SeasonsService } from '../../core/services/seasons.service';
import {
  FixtureSelection,
  FixtureSelectionState,
} from '../../shared/components/fixture-selection/fixture-selection';
import { isoDateFromToday, toImportItem } from '../../shared/utils/fixture.util';
import { ordinalRoundName } from '../../shared/utils/round-name.util';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-round-form',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, FixtureSelection],
  template: `
    <div class="mb-3">
      <div class="page-trail">
        <a routerLink="/admin">Admin</a> ·
        <a routerLink="/admin/rounds">{{ 'nav.rounds' | translate }}</a> ·
        {{ 'adminRounds.new' | translate }}
      </div>
      <h1 class="h4 fw-bold mb-1">{{ 'roundForm.title' | translate }}</h1>
      <p class="text-muted small mb-0">{{ 'roundForm.subtitle' | translate }}</p>
    </div>

    <div class="card mb-3">
      <div class="card-body p-4">
        <form [formGroup]="form" class="vstack gap-3">
          <div>
            <label class="form-label">{{ 'roundForm.season' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text">🏆</span>
              <select class="form-select" formControlName="seasonId">
                <option value="">{{ 'roundForm.select' | translate }}</option>
                @for (s of seasons(); track s.id) {
                  <option [value]="s.id">{{ s.name }}{{ s.isActive ? ' ✓' : '' }}</option>
                }
              </select>
            </div>
          </div>

          <div>
            <label class="form-label">{{ 'roundForm.number' | translate }}</label>
            <div class="input-group input-group-lg">
              <span class="input-group-text">#</span>
              <input type="number" min="1" class="form-control" formControlName="number" />
            </div>
          </div>

          <div>
            <label class="form-label"
              >{{ 'roundForm.name' | translate }} {{ 'common.optional' | translate }}</label
            >
            <div class="input-group input-group-lg">
              <span class="input-group-text">🏷️</span>
              <input
                class="form-control"
                formControlName="title"
                [placeholder]="'roundForm.namePlaceholder' | translate"
              />
            </div>
          </div>

          <div class="row g-3">
            <div class="col-6">
              <label class="form-label">{{ 'roundForm.startDate' | translate }}</label>
              <div class="input-group input-group-lg">
                <span class="input-group-text">📅</span>
                <input type="date" class="form-control" formControlName="startDate" />
              </div>
            </div>
            <div class="col-6">
              <label class="form-label">{{ 'roundForm.endDate' | translate }}</label>
              <div class="input-group input-group-lg">
                <span class="input-group-text">🗓️</span>
                <input type="date" class="form-control" formControlName="endDate" />
              </div>
            </div>
          </div>
          @if (dateRangeInvalid()) {
            <div class="text-danger small">{{ 'fixtures.endBeforeStart' | translate }}</div>
          }

          <hr class="my-1" />

          <button
            type="button"
            class="btn btn-soft-primary btn-lg w-100"
            [disabled]="!canSearch() || searching()"
            (click)="search()"
          >
            @if (searching()) {
              <span class="spinner-border spinner-border-sm me-2"></span>
            }
            🔍 {{ 'fixtures.searchButton' | translate }}
          </button>
        </form>
      </div>
    </div>

    @if (searched()) {
      <div class="card mb-3">
        <div class="card-body">
          <app-fixture-selection
            [fixtures]="fixtures()"
            [source]="source()"
            (selectionChange)="onSelection($event)"
          />
        </div>
      </div>
    }

    @if (searched() && fixtures().length === 0) {
      <div class="alert alert-info py-2">{{ 'fixtures.noneFoundManual' | translate }}</div>
    }

    <button
      type="button"
      class="btn btn-primary btn-lg w-100"
      [disabled]="form.invalid || dateRangeInvalid() || saving() || !selection().canSave"
      (click)="save()"
    >
      {{
        (selection().items.length === 0 ? 'fixtures.createManual' : 'roundForm.createWithFixtures')
          | translate: { count: selection().items.length }
      }}
    </button>

    <p class="text-muted small mt-2 mb-0">{{ 'fixtures.manualHint' | translate }}</p>
  `,
})
export class AdminRoundForm implements OnInit {
  private readonly seasonsApi = inject(SeasonsService);
  private readonly roundsApi = inject(RoundsService);
  private readonly adminApi = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly seasons = signal<Season[]>([]);
  private readonly rounds = signal<RoundSummary[]>([]);
  /** While true, the title auto-follows the number ("Primeira Rodada", …). */
  private autoTitle = true;
  protected readonly saving = signal(false);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  protected readonly source = signal('');
  protected readonly fixtures = signal<FixtureCandidate[]>([]);
  protected readonly selection = signal<FixtureSelectionState>({
    items: [],
    leagueOneJustification: null,
    canSave: true,
  });

  protected readonly form = this.fb.nonNullable.group({
    seasonId: ['', Validators.required],
    number: [1, [Validators.required, Validators.min(1)]],
    title: [''],
    // Default the round window to today → +10 days (admin can adjust).
    startDate: [isoDateFromToday(0), Validators.required],
    endDate: [isoDateFromToday(10), Validators.required],
  });

  constructor() {
    // Keep the name in sync with the number while it's still auto-generated.
    this.form.controls.number.valueChanges.pipe(takeUntilDestroyed()).subscribe((n) => {
      if (this.autoTitle && n) {
        this.form.controls.title.setValue(ordinalRoundName(n, this.language.current()), {
          emitEvent: false,
        });
      }
    });
    // A manual edit of the name stops the auto-sync.
    this.form.controls.title.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.autoTitle = false;
    });
    // Re-number sequentially whenever the season changes.
    this.form.controls.seasonId.valueChanges.pipe(takeUntilDestroyed()).subscribe((seasonId) => {
      this.applyNextNumber(seasonId);
    });
  }

  ngOnInit(): void {
    forkJoin({ seasons: this.seasonsApi.list(), rounds: this.roundsApi.getAll() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ seasons, rounds }) => {
        this.seasons.set(seasons);
        this.rounds.set(Array.isArray(rounds) ? rounds : []);
        const active = seasons.find((s) => s.isActive);
        if (active) {
          // Cascades: seasonId → next number → auto name.
          this.form.patchValue({ seasonId: active.id });
        } else {
          this.applyNextNumber('');
        }
      });
    // Pre-search the default window (today → +10) so suggestions are ready.
    this.preSearch();
  }

  /** Next sequential number for a season = highest existing + 1 (or 1). */
  private applyNextNumber(seasonId: string): void {
    const numbers = this.rounds()
      .filter((r) => r.seasonId === seasonId)
      .map((r) => r.number);
    const next = numbers.length ? Math.max(...numbers) + 1 : 1;
    this.form.controls.number.setValue(next, { emitEvent: false });
    if (this.autoTitle) {
      this.form.controls.title.setValue(ordinalRoundName(next, this.language.current()), {
        emitEvent: false,
      });
    }
  }

  /**
   * Automatic pre-search on load: populates the fixture list silently (no error
   * toast if the source is down) and only reveals it when there are results.
   */
  private preSearch(): void {
    if (!this.canSearch()) return;
    const { startDate, endDate } = this.form.getRawValue();
    this.searching.set(true);
    this.adminApi
      .searchFixtures(
        { startDate: `${startDate}T00:00:00`, endDate: `${endDate}T23:59:59` },
        { silent: true },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if ((res.fixtures?.length ?? 0) > 0) {
            this.fixtures.set(res.fixtures);
            this.source.set(res.source);
            this.searched.set(true);
          }
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });
  }

  // --- Date helpers -------------------------------------------------------
  protected dateRangeInvalid(): boolean {
    const { startDate, endDate } = this.form.getRawValue();
    return !!startDate && !!endDate && endDate < startDate;
  }

  protected canSearch(): boolean {
    const { startDate, endDate } = this.form.getRawValue();
    return !!startDate && !!endDate && !this.dateRangeInvalid();
  }

  // --- Search -------------------------------------------------------------
  protected search(): void {
    if (!this.canSearch()) return;
    const { startDate, endDate } = this.form.getRawValue();
    this.searching.set(true);
    this.adminApi
      .searchFixtures({ startDate: `${startDate}T00:00:00`, endDate: `${endDate}T23:59:59` })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.fixtures.set(res.fixtures);
          this.source.set(res.source);
          this.searched.set(true);
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });
  }

  protected onSelection(state: FixtureSelectionState): void {
    this.selection.set(state);
  }

  // --- Save ---------------------------------------------------------------
  protected save(): void {
    const state = this.selection();
    if (this.form.invalid || this.dateRangeInvalid() || !state.canSave) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { seasonId, number, title, startDate, endDate } = this.form.getRawValue();
    this.roundsApi
      .create({
        seasonId,
        number,
        title: title || null,
        startDate: `${startDate}T00:00:00`,
        endDate: `${endDate}T23:59:59`,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (round) => {
          if (state.items.length === 0) {
            // No fixtures picked/found — land on the manual add/edit screen.
            this.toast.success(this.translate.instant('roundForm.created'));
            this.router.navigate(['/admin/rounds', round.id, 'matches']);
            return;
          }
          this.adminApi
            .importFixtures(round.id, {
              fixtures: state.items.map(toImportItem),
              leagueOneJustification: state.leagueOneJustification,
            })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (res) => {
                this.toast.success(
                  this.translate.instant('fixtures.importedToast', {
                    imported: res.importedCount,
                    skipped: res.skippedDuplicateCount,
                  }),
                );
                this.router.navigate(['/admin/rounds', round.id]);
              },
              error: () => {
                // Round was created; let the admin finish on the detail screen.
                this.saving.set(false);
                this.router.navigate(['/admin/rounds', round.id]);
              },
            });
        },
        error: () => this.saving.set(false),
      });
  }
}
