import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Standing } from '@core/models';
import { avatarColor, initials } from '@shared/utils/avatar.util';

/**
 * The top three of a standings table, shown only when there are three. Used by the in-app
 * standings and the public link; the look comes from the global `.podium` styles.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-podium',
  imports: [TranslatePipe],
  template: `
    @if (entries().length === 3) {
      <div class="podium mb-3" [class.fade-in-up]="animated()">
        @for (p of entries(); track p.userId) {
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
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
})
export class Podium {
  /** The first three rows of the standings. */
  readonly entries = input.required<readonly Standing[]>();
  /** Whose slot to highlight, if any. */
  readonly meId = input<string | null | undefined>(null);
  readonly animated = input(false);

  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;
}
