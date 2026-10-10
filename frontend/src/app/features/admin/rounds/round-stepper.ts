import { Component, ChangeDetectionStrategy, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RoundStatus } from '@core/models/enums';
import { Icon } from '@shared/components/icon/icon';

type StepState = 'done' | 'current' | 'upcoming';
interface Step {
  status: RoundStatus;
  state: StepState;
}

/**
 * Visual timeline of the round lifecycle (Draft → Published → Locked → Scored).
 * Highlights the current step, checks completed ones and mutes upcoming ones, so
 * the admin always sees where the round is and what comes next. A cancelled round
 * shows a distinct banner instead of the progression.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-round-stepper',
  imports: [TranslatePipe, Icon],
  template: `
    @if (status() === RoundStatus.Cancelled) {
      <div class="alert alert-danger py-2 mb-0">
        {{ 'status.Cancelled' | translate }}
      </div>
    } @else {
      <ol class="round-stepper">
        @for (step of steps(); track step.status; let last = $last) {
          <li class="round-stepper__step" [attr.data-state]="step.state">
            <span class="round-stepper__dot">
              @if (step.state === 'done') {
                <app-icon name="check" [size]="16" />
              } @else {
                {{ $index + 1 }}
              }
            </span>
            <span class="round-stepper__label">{{ 'status.' + step.status | translate }}</span>
            @if (!last) {
              <span class="round-stepper__bar" aria-hidden="true"></span>
            }
          </li>
        }
      </ol>
    }
  `,
  styleUrl: './round-stepper.scss',
})
export class RoundStepper {
  readonly status = input.required<RoundStatus>();
  protected readonly RoundStatus = RoundStatus;

  /** Ordered lifecycle stages (Cancelled is handled separately, not a step). */
  private readonly order: RoundStatus[] = [
    RoundStatus.Draft,
    RoundStatus.Published,
    RoundStatus.Locked,
    RoundStatus.Scored,
  ];

  protected readonly steps = computed<Step[]>(() => {
    const currentIndex = this.order.indexOf(this.status());
    return this.order.map((status, i) => ({
      status,
      state: i < currentIndex ? 'done' : i === currentIndex ? 'current' : 'upcoming',
    }));
  });
}
