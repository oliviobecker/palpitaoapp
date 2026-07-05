import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it } from 'vitest';
import { FormField } from './form-field';

@Component({
  imports: [FormField, ReactiveFormsModule],
  template: `
    <app-form-field
      label="Name"
      forId="name"
      [control]="control"
      [errors]="{ required: 'Name is required.' }"
      hint="A hint"
      [forceError]="forceError"
    >
      <input id="name" class="form-control" [formControl]="control" />
    </app-form-field>
  `,
})
class Host {
  control = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  forceError = '';
}

describe('FormField', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
  });

  function render() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the label and the hint while the control is untouched', () => {
    const fixture = render();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('label')?.textContent).toContain('Name');
    expect(el.querySelector('.form-text')?.textContent).toContain('A hint');
    expect(el.querySelector('.invalid-feedback')).toBeNull();
  });

  it('shows the mapped error after the control is touched', async () => {
    const fixture = render();
    fixture.componentInstance.control.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.invalid-feedback')?.textContent).toContain('Name is required.');
    expect(el.querySelector('app-form-field')?.classList.contains('is-invalid')).toBe(true);
  });

  it('hides the error once the control becomes valid', async () => {
    const fixture = render();
    fixture.componentInstance.control.markAsTouched();
    fixture.componentInstance.control.setValue('João');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.invalid-feedback')).toBeNull();
    expect(el.querySelector('app-form-field')?.classList.contains('is-invalid')).toBe(false);
  });

  it('shows a forced cross-field error even when the control is valid', () => {
    const fixture = render();
    fixture.componentInstance.control.setValue('João');
    fixture.componentInstance.forceError = 'Passwords do not match.';
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.invalid-feedback')?.textContent).toContain(
      'Passwords do not match.',
    );
  });
});
