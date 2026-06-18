import { FormBuilder, Validators } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { passwordsMatch } from './register';

const fb = new FormBuilder();

function buildForm() {
  return fb.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );
}

describe('register form', () => {
  it('is invalid while required fields are empty', () => {
    const form = buildForm();
    expect(form.valid).toBe(false);
    expect(form.controls.name.hasError('required')).toBe(true);
    expect(form.controls.email.hasError('required')).toBe(true);
  });

  it('rejects an invalid email', () => {
    const form = buildForm();
    form.controls.email.setValue('not-an-email');
    expect(form.controls.email.hasError('email')).toBe(true);
  });

  it('rejects weak passwords (needs 8+ chars, a letter and a number)', () => {
    const form = buildForm();
    for (const weak of ['Curta1', 'semnumeros', '12345678']) {
      form.controls.password.setValue(weak);
      expect(form.controls.password.valid).toBe(false);
    }
    form.controls.password.setValue('Senha123');
    expect(form.controls.password.valid).toBe(true);
  });

  it('flags mismatched password confirmation at form level', () => {
    const form = buildForm();
    form.patchValue({
      name: 'João',
      email: 'joao@x.com',
      password: 'Senha123',
      confirmPassword: 'Outra123',
    });
    expect(form.hasError('passwordMismatch')).toBe(true);
    expect(form.valid).toBe(false);

    form.controls.confirmPassword.setValue('Senha123');
    expect(form.hasError('passwordMismatch')).toBe(false);
    expect(form.valid).toBe(true);
  });
});
