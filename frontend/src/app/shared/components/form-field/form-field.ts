import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  signal,
} from '@angular/core';
import { AbstractControl } from '@angular/forms';

/**
 * Standard wrapper for a form control: label above, projected input/select
 * (plain or inside an input-group), translated error below when the control
 * is invalid and touched/dirty, otherwise an optional hint.
 *
 * Usage:
 *   <app-form-field
 *     [label]="'x.name' | translate"
 *     forId="name"
 *     [control]="form.controls.name"
 *     [errors]="{ required: 'validation.nameRequired' | translate }"
 *   >
 *     <input id="name" class="form-control" formControlName="name" />
 *   </app-form-field>
 *
 * The host gets `.is-invalid` while the error shows; styles.scss colours the
 * projected `.form-control`/`.form-select` borders from that class, so callers
 * don't need per-input `[class.is-invalid]` bindings.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-form-field',
  host: { class: 'd-block', '[class.is-invalid]': 'invalid()' },
  template: `
    @if (label()) {
      <label class="form-label" [attr.for]="forId() || null">{{ label() }}</label>
    }
    <ng-content />
    @if (errorText(); as message) {
      <div class="invalid-feedback d-block">{{ message }}</div>
    } @else if (hint()) {
      <div class="form-text">{{ hint() }}</div>
    }
  `,
})
export class FormField {
  readonly label = input('');
  readonly forId = input('');
  readonly hint = input('');
  readonly control = input<AbstractControl | null>(null);
  /** Validator error key (or 'default') → translated message. */
  readonly errors = input<Record<string, string>>({});
  /** Cross-field/manual error evaluated by the parent; shown as-is when set. */
  readonly forceError = input('');

  /** Bumped on every control event so computeds re-read the control state (OnPush-safe). */
  private readonly controlVersion = signal(0);

  constructor() {
    effect((onCleanup) => {
      const control = this.control();
      if (!control) return;
      const sub = control.events.subscribe(() => this.controlVersion.update((v) => v + 1));
      onCleanup(() => sub.unsubscribe());
    });
  }

  private readonly controlInvalid = computed(() => {
    this.controlVersion();
    const control = this.control();
    return !!control && control.invalid && (control.touched || control.dirty);
  });

  protected readonly invalid = computed(() => this.forceError() !== '' || this.controlInvalid());

  protected readonly errorText = computed(() => {
    const forced = this.forceError();
    if (forced) return forced;
    if (!this.controlInvalid()) return '';
    const failed = this.control()?.errors ?? {};
    const messages = this.errors();
    for (const key of Object.keys(failed)) {
      if (messages[key]) return messages[key];
    }
    return messages['default'] ?? '';
  });
}
