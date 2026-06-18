import { Component, ChangeDetectionStrategy, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RoundStatus } from '../../core/models/enums';

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
  imports: [TranslatePipe],
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
                ✓
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
  styles: [
    `
      .round-stepper {
        display: flex;
        list-style: none;
        padding: 0;
        margin: 0;
      }
      .round-stepper__step {
        position: relative;
        flex: 1 1 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.35rem;
        text-align: center;
        color: var(--bs-secondary-color, #6c757d);
        font-size: 0.8rem;
      }
      .round-stepper__dot {
        width: 1.9rem;
        height: 1.9rem;
        border-radius: 50%;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        font-weight: 700;
        background: var(--bs-secondary-bg, #e9ecef);
        color: var(--bs-secondary-color, #6c757d);
        border: 2px solid transparent;
        z-index: 1;
      }
      .round-stepper__bar {
        position: absolute;
        top: 0.95rem;
        left: 50%;
        width: 100%;
        height: 2px;
        background: var(--bs-border-color, #dee2e6);
      }
      .round-stepper__step[data-state='done'] {
        color: var(--bs-success, #198754);
      }
      .round-stepper__step[data-state='done'] .round-stepper__dot {
        background: var(--bs-success, #198754);
        color: #fff;
      }
      .round-stepper__step[data-state='done'] .round-stepper__bar {
        background: var(--bs-success, #198754);
      }
      .round-stepper__step[data-state='current'] {
        color: var(--bs-primary, #0d6efd);
        font-weight: 700;
      }
      .round-stepper__step[data-state='current'] .round-stepper__dot {
        background: var(--bs-primary, #0d6efd);
        color: #fff;
        border-color: var(--bs-primary, #0d6efd);
      }
    `,
  ],
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
