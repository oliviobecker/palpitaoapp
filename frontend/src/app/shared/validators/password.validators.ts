import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

/** The backend's PasswordPolicy, client side: 8+ characters with at least a letter and a digit. */
export const PASSWORD_PATTERN = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/;

/** A required, strong password — reports `required`, then `pattern`. */
export const strongPassword: ValidatorFn[] = [
  Validators.required,
  Validators.pattern(PASSWORD_PATTERN),
];

/** Form-level validator: `confirmPassword` must equal `password` — reports `passwordMismatch`. */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}
