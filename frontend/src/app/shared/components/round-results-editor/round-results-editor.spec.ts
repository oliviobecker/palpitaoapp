import { FormBuilder } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { completePairs, scorePairValidator } from './round-results-editor';

const fb = new FormBuilder();

function pair(home: number | null, away: number | null) {
  return fb.group({ home: [home], away: [away] }, { validators: scorePairValidator });
}

describe('scorePairValidator', () => {
  it('accepts an empty pair (match not played yet)', () => {
    expect(pair(null, null).hasError('partialPair')).toBe(false);
  });

  it('accepts a complete pair, including 0x0', () => {
    expect(pair(0, 0).hasError('partialPair')).toBe(false);
    expect(pair(2, 1).hasError('partialPair')).toBe(false);
  });

  it('flags a half-filled pair', () => {
    expect(pair(1, null).hasError('partialPair')).toBe(true);
    expect(pair(null, 0).hasError('partialPair')).toBe(true);
  });
});

describe('completePairs', () => {
  it('returns only the indices with both scores present', () => {
    const values = [
      { home: 2, away: 1 }, // complete
      { home: null, away: null }, // untouched -> skipped
      { home: 0, away: 0 }, // complete (0x0 counts)
      { home: 3, away: null }, // partial -> skipped
    ];
    expect(completePairs(values)).toEqual([0, 2]);
  });

  it('treats empty strings as missing', () => {
    expect(completePairs([{ home: '', away: 1 }])).toEqual([]);
  });
});
