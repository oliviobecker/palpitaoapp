import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Both scores filled, or neither — a half-filled pair cannot be saved. */
export function scorePairValidator(group: AbstractControl): ValidationErrors | null {
  const home = group.get('home')?.value;
  const away = group.get('away')?.value;
  const filled = [home, away].filter((v) => v !== null && v !== '').length;
  return filled === 1 ? { partialPair: true } : null;
}

/** Indices of the pairs that are complete (both scores present). */
export function completePairs(values: { home: unknown; away: unknown }[]): number[] {
  return values.flatMap((v, i) =>
    v.home !== null && v.home !== '' && v.away !== null && v.away !== '' ? [i] : [],
  );
}

/**
 * Indices of the pairs to save: complete, and typed by the admin. A score the form only shows —
 * what the results refresh brought in, a live one included — is never sent: saving it would turn
 * it into a manual final result, which the refresh then leaves alone for good.
 */
export function pairsToSave(
  values: { home: unknown; away: unknown }[],
  edited: readonly boolean[],
): number[] {
  return completePairs(values).filter((i) => edited[i]);
}
