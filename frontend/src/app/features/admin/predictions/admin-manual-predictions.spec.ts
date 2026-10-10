import { describe, expect, it } from 'vitest';
import { manualPredictionItems, missingScoreCount, typedScore } from './admin-manual-predictions';

describe('typedScore', () => {
  it('keeps a typed number, including 0', () => {
    expect(typedScore(0)).toBe(0);
    expect(typedScore(3)).toBe(3);
    expect(typedScore('2')).toBe(2);
  });

  it('reads an empty box as missing, never as 0', () => {
    expect(typedScore(null)).toBeNull();
    expect(typedScore(undefined)).toBeNull();
    expect(typedScore('')).toBeNull();
    expect(typedScore('  ')).toBeNull();
    expect(typedScore('abc')).toBeNull();
  });
});

describe('missingScoreCount', () => {
  it('counts the matches with either box still empty', () => {
    const entries = [
      { home: 2, away: 1 },
      { home: null, away: null },
      { home: 0, away: 0 },
      { home: 3, away: null },
    ];
    expect(missingScoreCount(entries)).toBe(2);
  });

  it('is zero once every box is typed', () => {
    expect(missingScoreCount([{ home: 0, away: 0 }])).toBe(0);
  });
});

describe('manualPredictionItems', () => {
  const matches = [{ id: 'm1' }, { id: 'm2' }];

  it('builds one row per match from the typed scores', () => {
    expect(
      manualPredictionItems(matches, [
        { home: 2, away: 1 },
        { home: 0, away: 0 },
      ]),
    ).toEqual([
      { roundMatchId: 'm1', predictedHomeScore: 2, predictedAwayScore: 1 },
      { roundMatchId: 'm2', predictedHomeScore: 0, predictedAwayScore: 0 },
    ]);
  });

  it('refuses the whole set while any box is empty, instead of sending it as 0', () => {
    // Production 23/09/2026: a line the OCR import missed went out as a 0x0 nobody typed.
    expect(
      manualPredictionItems(matches, [
        { home: 2, away: 1 },
        { home: null, away: null },
      ]),
    ).toBeNull();
    expect(
      manualPredictionItems(matches, [
        { home: 2, away: 1 },
        { home: 1, away: '' },
      ]),
    ).toBeNull();
  });

  it('refuses a set with fewer score rows than matches', () => {
    expect(manualPredictionItems(matches, [{ home: 2, away: 1 }])).toBeNull();
  });
});
