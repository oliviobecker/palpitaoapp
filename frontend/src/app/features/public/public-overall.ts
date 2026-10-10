import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { PublicStandingRow } from '@core/models';
import { EmptyState } from '@shared/components/empty-state/empty-state';
import { Icon } from '@shared/components/icon/icon';
import { RoundLabelPipe } from '@shared/pipes/round-label.pipe';
import { avatarColor, initials } from '@shared/utils/avatar.util';
import { NumberedRound } from '@shared/utils/round-name.util';
import { RankedRow } from './public-standings.util';

/**
 * The season standings of the public link: podium, name search and one expandable card per
 * participant with their gaps and round history. Presentational — the page owns the state.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-public-overall',
  imports: [TranslatePipe, EmptyState, Icon, RoundLabelPipe],
  styles: `
    :host {
      display: contents;
    }
  `,
  template: `
    @if (podium().length === 3) {
      <div class="podium mb-3">
        @for (p of podium(); track p.userId) {
          <div
            class="podium__slot podium__slot--{{ p.position }}"
            [class.podium__slot--me]="p.userId === meId()"
          >
            <span class="podium__rank">{{ p.position }}</span>
            <span class="podium__avatar" [style.background]="avatarColor(p.name)">{{
              initials(p.name)
            }}</span>
            <div class="podium__name text-truncate">{{ p.name }}</div>
            <div class="podium__pts">
              {{ p.totalPoints }} <small>{{ 'dashboard.pts' | translate }}</small>
            </div>
          </div>
        }
      </div>
    }

    <div class="mb-2">
      <input
        type="search"
        class="form-control form-control-sm"
        [value]="filter()"
        (input)="filterChange.emit($any($event.target).value)"
        [placeholder]="'publicStandings.findName' | translate"
        [attr.aria-label]="'publicStandings.findName' | translate"
      />
      @if (filter().trim()) {
        <div class="form-text">
          {{ 'publicStandings.showingOf' | translate: { shown: rows().length, total: total() } }}
        </div>
      }
    </div>

    <!-- Column labels: desktop only, where there is room for the numbers the phone
         keeps inside the expansion. -->
    <div class="d-none d-md-flex small text-muted px-3 pb-1">
      <span class="ms-auto d-flex gap-3 text-end">
        <span style="min-width: 3.5rem">{{ 'standings.rounds' | translate }}</span>
        <span style="min-width: 3.5rem">{{ 'standings.absences' | translate }}</span>
        <span style="min-width: 3.5rem">{{ 'publicStandings.diff' | translate }}</span>
        <span style="min-width: 3.5rem">{{ 'standings.points' | translate }}</span>
        <span style="width: 1rem"></span>
      </span>
    </div>

    @if (rows().length === 0) {
      <app-empty-state icon="search" [message]="'publicStandings.noMatch' | translate" />
    } @else {
      <div class="vstack gap-2">
        @for (row of rows(); track row.userId) {
          <div
            class="card"
            [class.border-danger]="row.isEliminated"
            [class.border-primary]="row.userId === meId()"
          >
            <button
              type="button"
              class="row-toggle card-body py-2 px-3 d-flex justify-content-between align-items-center w-100 border-0 bg-transparent text-start"
              [attr.aria-expanded]="isOpen(row.userId)"
              [attr.aria-controls]="'overall-' + row.userId"
              (click)="rowToggle.emit(row.userId)"
            >
              <span class="d-flex align-items-center gap-2" style="min-width: 0">
                <span class="text-muted small" style="min-width: 1.5rem">{{ row.position }}</span>
                <span class="rank-avatar" [style.background]="avatarColor(row.name)">{{
                  initials(row.name)
                }}</span>
                <span class="fw-semibold text-truncate">{{ row.name }}</span>
                @if (row.userId === meId()) {
                  <span class="badge text-bg-primary">{{ 'common.you' | translate }}</span>
                }
                @if (row.isEliminated) {
                  <span class="badge text-bg-danger">{{ 'standings.eliminated' | translate }}</span>
                }
              </span>
              <span class="d-flex align-items-center gap-3 flex-shrink-0">
                <span class="d-none d-md-flex gap-3 small text-muted calc text-end">
                  <span style="min-width: 3.5rem">{{ row.playedRounds }}</span>
                  <span style="min-width: 3.5rem">{{ row.absenceCount }}</span>
                  <span style="min-width: 3.5rem">{{
                    row.toLeader > 0 ? '−' + row.toLeader : '—'
                  }}</span>
                </span>
                <span class="h6 fw-bold mb-0 calc text-end" style="min-width: 3.5rem">{{
                  row.totalPoints
                }}</span>
                <app-icon
                  [name]="isOpen(row.userId) ? 'chevron-down' : 'chevron-right'"
                  [size]="16"
                />
              </span>
            </button>

            @if (isOpen(row.userId)) {
              <div class="card-body pt-0 px-3 small" [id]="'overall-' + row.userId">
                <div class="d-flex flex-wrap gap-3 text-muted">
                  <span>{{ 'standings.rounds' | translate }}: {{ row.playedRounds }}</span>
                  <span>{{ 'standings.absences' | translate }}: {{ row.absenceCount }}</span>
                  @if (row.penaltyPoints > 0) {
                    <span class="text-danger"
                      >{{ 'standings.penalties' | translate }}: −{{ row.penaltyPoints }}</span
                    >
                  }
                </div>
                @if (row.toLeader > 0) {
                  <div class="text-muted mt-1">
                    {{ 'publicStandings.behindLeader' | translate: { n: row.toLeader } }}
                    @if (row.above) {
                      &middot;
                      {{
                        'publicStandings.behindAbove'
                          | translate: { n: row.toAbove, name: row.above }
                      }}
                    }
                  </div>
                }
                @if (row.rounds.length > 0) {
                  <div class="mt-2">
                    <div class="text-muted mb-1">
                      {{ 'publicStandings.roundHistory' | translate }}
                    </div>
                    <div class="d-flex flex-wrap gap-1">
                      @for (h of row.rounds; track h.number + '.' + (h.part ?? 0)) {
                        <button
                          type="button"
                          class="btn btn-sm btn-outline-secondary py-0 px-2 calc"
                          [class.border-warning]="h.flavioRuleApplied"
                          [class.text-muted]="h.wasAbsent"
                          [attr.title]="
                            'publicStandings.roundN'
                              | translate: { n: (h.number | roundLabel: h.part) }
                          "
                          (click)="audit.emit({ userId: row.userId, round: h })"
                        >
                          <span class="text-muted">{{ h.number | roundLabel: h.part }}</span>
                          <span class="text-muted mx-1">·</span>
                          <span class="fw-semibold">{{ h.points }}</span>
                          @if (h.wasAbsent) {
                            <app-icon name="ban" [size]="12" class="ms-1" />
                          }
                        </button>
                      }
                    </div>
                  </div>
                }
                <div class="d-flex flex-wrap gap-2 mt-2">
                  @if (hasRounds()) {
                    <button
                      type="button"
                      class="btn btn-sm btn-outline-secondary"
                      (click)="audit.emit({ userId: row.userId })"
                    >
                      {{ 'publicStandings.seeRoundDetail' | translate }}
                    </button>
                  }
                  <button
                    type="button"
                    class="btn btn-sm"
                    [class.btn-primary]="row.userId === meId()"
                    [class.btn-outline-secondary]="row.userId !== meId()"
                    (click)="meToggle.emit(row.userId)"
                  >
                    {{
                      (row.userId === meId()
                        ? 'publicStandings.unmarkMe'
                        : 'publicStandings.markMe'
                      ) | translate
                    }}
                  </button>
                </div>
              </div>
            }
          </div>
        }
      </div>
    }
  `,
})
export class PublicOverall {
  readonly rows = input.required<RankedRow[]>();
  /** How many rows there are before the name filter. */
  readonly total = input.required<number>();
  readonly podium = input.required<PublicStandingRow[]>();
  readonly meId = input<string | null>(null);
  readonly filter = input('');
  readonly hasRounds = input(false);
  readonly expanded = input.required<ReadonlySet<string>>();

  readonly filterChange = output<string>();
  readonly rowToggle = output<string>();
  /** Open this participant's round breakdown, at a given round or the current one. */
  readonly audit = output<{ userId: string; round?: NumberedRound }>();
  readonly meToggle = output<string>();

  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;

  protected isOpen(userId: string): boolean {
    return this.expanded().has(userId);
  }
}
