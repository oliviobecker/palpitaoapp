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

  it('resolves the ticked ids when a choices confirmation is confirmed', async () => {
    const svc = new ConfirmService();
    const result = svc.askWithChoices(
      'Activate John?',
      [
        { id: 'r1', label: 'Round 1', checked: true },
        { id: 'r2', label: 'Round 2' },
      ],
      { choicesLabel: 'Record as absent in:' },
    );

    expect(svc.state().choices).toHaveLength(2);
    expect(svc.state().choicesLabel).toBe('Record as absent in:');
    expect(svc.state().withInput).toBe(false);
    svc.confirm(undefined, ['r1']);

    await expect(result).resolves.toEqual({ text: '', choiceIds: ['r1'] });
    expect(svc.state().open).toBe(false);
  });

  it('treats an empty selection as a deliberate confirmation, not a cancel', async () => {
    const svc = new ConfirmService();
    const result = svc.askWithChoices('Activate John?', [{ id: 'r1', label: 'Round 1' }]);
    svc.confirm(undefined, []);

    await expect(result).resolves.toEqual({ text: '', choiceIds: [] });
  });

  it('resolves null when a choices confirmation is cancelled', async () => {
    const svc = new ConfirmService();
    const result = svc.askWithChoices('Activate John?', [{ id: 'r1', label: 'Round 1' }]);
    svc.cancel();

    await expect(result).resolves.toBeNull();
  });

  it('combines the checkbox list with a justification textarea', async () => {
    const svc = new ConfirmService();
    const result = svc.askWithChoices('Reactivate John?', [{ id: 'r1', label: 'Round 1' }], {
      withInput: true,
      inputLabel: 'Justification',
    });

    expect(svc.state().withInput).toBe(true);
    expect(svc.state().choices).toHaveLength(1);
    svc.confirm('  back in  ', ['r1']);

    await expect(result).resolves.toEqual({ text: 'back in', choiceIds: ['r1'] });
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
