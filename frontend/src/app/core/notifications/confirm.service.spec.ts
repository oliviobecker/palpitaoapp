import { describe, expect, it } from 'vitest';
import { ConfirmService } from './confirm.service';

describe('ConfirmService', () => {
  it('resolves true when a plain confirmation is confirmed', async () => {
    const svc = new ConfirmService();
    const result = svc.ask('Continue?');

    expect(svc.state().open).toBe(true);
    expect(svc.state().withInput).toBe(false);
    svc.confirm();

    await expect(result).resolves.toBe(true);
    expect(svc.state().open).toBe(false);
  });

  it('resolves false when a plain confirmation is cancelled', async () => {
    const svc = new ConfirmService();
    const result = svc.ask('Continue?');
    svc.cancel();

    await expect(result).resolves.toBe(false);
  });

  it('resolves the trimmed input when confirmed with text', async () => {
    const svc = new ConfirmService();
    const result = svc.askWithInput('Why?', { inputLabel: 'Justification' });

    expect(svc.state().withInput).toBe(true);
    expect(svc.state().inputRequired).toBe(true);
    svc.confirm('  because  ');

    await expect(result).resolves.toBe('because');
    expect(svc.state().open).toBe(false);
  });

  it('resolves null when an input confirmation is cancelled', async () => {
    const svc = new ConfirmService();
    const result = svc.askWithInput('Why?');
    svc.cancel();

    await expect(result).resolves.toBeNull();
  });

  it('carries the danger flag and texts into the state', () => {
    const svc = new ConfirmService();
    void svc.askWithInput('Eliminate John?', {
      title: 'Eliminate',
      confirmText: 'Eliminate',
      danger: true,
      inputLabel: 'Justification',
    });

    expect(svc.state().title).toBe('Eliminate');
    expect(svc.state().confirmText).toBe('Eliminate');
    expect(svc.state().danger).toBe(true);
    expect(svc.state().inputLabel).toBe('Justification');
    svc.cancel();
  });
});
