import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { passwordsMatch, strongPassword } from './password.validators';

describe('password validators', () => {
  it('requires a password', () => {
    expect(new FormControl('', strongPassword).hasError('required')).toBe(true);
  });

  it.each(['Curta1', 'semnumeros', '12345678'])('rejects the weak password %s', (weak) => {
    expect(new FormControl(weak, strongPassword).hasError('pattern')).toBe(true);
  });

  it('accepts 8+ characters with a letter and a digit', () => {
    expect(new FormControl('Senha123', strongPassword).valid).toBe(true);
  });

  it('flags a confirmation that differs, and only once both are typed', () => {
    const form = new FormGroup(
      { password: new FormControl('Senha123'), confirmPassword: new FormControl('') },
      { validators: passwordsMatch },
    );
    expect(form.hasError('passwordMismatch')).toBe(false);

    form.controls.confirmPassword.setValue('Outra123');
    expect(form.hasError('passwordMismatch')).toBe(true);

    form.controls.confirmPassword.setValue('Senha123');
    expect(form.hasError('passwordMismatch')).toBe(false);
  });
});
