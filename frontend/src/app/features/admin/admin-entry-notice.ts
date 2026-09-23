import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Round } from '../../core/models/models';
import { Icon } from '../../shared/components/icon/icon';
import { adminEntryBlockKey, flavioLateNotice } from './admin-entry.util';

/**
 * Heads the manual-entry and OCR-import screens: why entry is closed (the round was finalized,
 * is still a draft or was cancelled), or — while it is open — that a Flávio leader entered now
 * counts as late. Both link back to the round, where Reopen and the exemption panel live.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-admin-entry-notice',
  imports: [DatePipe, RouterLink, TranslatePipe, Icon],
  template: `
    @if (blockKey(); as key) {
      <div class="alert alert-warning d-flex gap-2 py-2" role="alert">
        <app-icon name="lock" [size]="16" class="mt-1" />
        <div>
          {{ key | translate }}
          <a class="alert-link" [routerLink]="['/admin/rounds', round().id]">{{
            'adminEntry.openRound' | translate
          }}</a>
        </div>
      </div>
    } @else if (flavio(); as f) {
      <div class="alert alert-info d-flex gap-2 py-2">
        <app-icon name="clock" [size]="16" class="mt-1" />
        <div>
          {{
            'adminEntry.flavioLate'
              | translate: { leaders: f.leaders, deadline: (f.deadlineUtc | date: 'dd/MM HH:mm') }
          }}
          <a class="alert-link" [routerLink]="['/admin/rounds', round().id]">{{
            'adminEntry.openRound' | translate
          }}</a>
        </div>
      </div>
    }
  `,
})
export class AdminEntryNotice {
  readonly round = input.required<Round>();

  protected readonly blockKey = computed(() => adminEntryBlockKey(this.round().status));
  protected readonly flavio = computed(() => flavioLateNotice(this.round()));
}
