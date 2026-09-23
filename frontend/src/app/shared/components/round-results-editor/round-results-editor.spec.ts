import { FormBuilder } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { completePairs, pairsToSave, scorePairValidator } from './round-results-editor';

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

describe('pairsToSave', () => {
  it('sends only the complete pairs the admin typed', () => {
    const values = [
      { home: 0, away: 0 }, // live score the refresh brought in, untouched -> never sent
      { home: 2, away: 1 }, // typed -> sent
      { home: 1, away: 1 }, // final score from the refresh, untouched -> not re-sent
      { home: 3, away: null }, // typed but half-filled -> not sent
    ];
    expect(pairsToSave(values, [false, true, false, true])).toEqual([1]);
  });

  it('sends a shown score once the admin retypes it', () => {
    expect(pairsToSave([{ home: 0, away: 1 }], [true])).toEqual([0]);
  });
});
