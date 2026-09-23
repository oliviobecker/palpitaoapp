import { describe, expect, it } from 'vitest';
import { RoundStatus } from '../../core/models/enums';
import { RoundFlavio } from '../../core/models/models';
import { adminEntryBlockKey, flavioLateNotice } from './admin-entry.util';

describe('adminEntryBlockKey', () => {
  it('keeps entry open on published and locked rounds, whatever the clock says', () => {
    expect(adminEntryBlockKey(RoundStatus.Published)).toBeNull();
    expect(adminEntryBlockKey(RoundStatus.Locked)).toBeNull();
  });

  it('closes entry once the round is finalized, and outside the published window', () => {
    expect(adminEntryBlockKey(RoundStatus.Scored)).toBe('adminEntry.blockedScored');
    expect(adminEntryBlockKey(RoundStatus.Draft)).toBe('adminEntry.blockedDraft');
    expect(adminEntryBlockKey(RoundStatus.Cancelled)).toBe('adminEntry.blockedCancelled');
  });

  it('does not block while the round is still loading', () => {
    expect(adminEntryBlockKey(undefined)).toBeNull();
  });
});

describe('flavioLateNotice', () => {
  const deadline = '2026-09-20T12:00:00Z';
  const at = (iso: string) => new Date(iso).getTime();
  const round = (flavio: Partial<RoundFlavio> | null) => ({
    flavio: flavio && {
      applies: true,
      leaderNames: ['Ana', 'Bia'],
      deadlineUtc: deadline,
      ...flavio,
    },
  });

  it('warns once the leader deadline has passed', () => {
    expect(flavioLateNotice(round({}), at('2026-09-20T12:00:01Z'))).toEqual({
      leaders: 'Ana, Bia',
      deadlineUtc: deadline,
    });
  });

  it('stays quiet before the leader deadline', () => {
    expect(flavioLateNotice(round({}), at('2026-09-20T11:59:59Z'))).toBeNull();
  });

  it('stays quiet when the rule does not apply or has no one to target', () => {
    const late = at('2026-09-21T00:00:00Z');
    expect(flavioLateNotice(round({ applies: false }), late)).toBeNull();
    expect(flavioLateNotice(round({ leaderNames: [] }), late)).toBeNull();
    expect(flavioLateNotice(round({ deadlineUtc: null }), late)).toBeNull();
    expect(flavioLateNotice(round(null), late)).toBeNull();
  });
});
