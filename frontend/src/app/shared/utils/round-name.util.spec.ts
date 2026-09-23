import { describe, expect, it } from 'vitest';
import { RoundStatus } from '../../core/models/enums';
import {
  compareRounds,
  joinPreview,
  ordinalRoundName,
  parseRoundLabel,
  roundLabel,
  sameRound,
} from './round-name.util';

describe('roundLabel', () => {
  it('writes a part after a dot and a standalone round bare', () => {
    expect(roundLabel(10)).toBe('10');
    expect(roundLabel(10, 0)).toBe('10');
    expect(roundLabel(10, null)).toBe('10');
    expect(roundLabel(10, 2)).toBe('10.2');
  });
});

describe('parseRoundLabel', () => {
  it('reads what roundLabel writes, part and all', () => {
    expect(parseRoundLabel('10')).toEqual({ number: 10, part: 0 });
    expect(parseRoundLabel('10.2')).toEqual({ number: 10, part: 2 });
    // A decimal read would collapse "10.10" into 10.1.
    expect(parseRoundLabel('10.10')).toEqual({ number: 10, part: 10 });
  });

  it('rejects anything that is not a label', () => {
    for (const bad of [null, undefined, '', 'x', '10.', '.2', '0', '-3', '10.2.1']) {
      expect(parseRoundLabel(bad)).toBeNull();
    }
  });
});

describe('compareRounds / sameRound', () => {
  it('orders by number, then part, a missing part counting as 0', () => {
    const rounds = [
      { number: 11 },
      { number: 10, part: 2 },
      { number: 10, part: 1 },
      { number: 9 },
    ];
    expect([...rounds].sort(compareRounds).map((r) => roundLabel(r.number, r.part))).toEqual([
      '9',
      '10.1',
      '10.2',
      '11',
    ]);
    expect(sameRound({ number: 10 }, { number: 10, part: 0 })).toBe(true);
    expect(sameRound({ number: 10, part: 1 }, { number: 10, part: 2 })).toBe(false);
  });
});

describe('joinPreview', () => {
  const r = (number: number, part = 0, status = RoundStatus.Scored) => ({ number, part, status });

  it('turns the previous standalone round into part 1 and lands after it', () => {
    expect(joinPreview([r(9), r(10)], 11)).toEqual({ number: 10, part: 2, scored: true });
  });

  it('appends to a round already in parts and skips a cancelled round in between', () => {
    const rounds = [r(10, 1), r(10, 2, RoundStatus.Draft), r(11, 0, RoundStatus.Cancelled)];
    expect(joinPreview(rounds, 12)).toEqual({ number: 10, part: 3, scored: true });
  });

  it('flags nothing to replay when no round of the target is scored', () => {
    expect(joinPreview([r(1, 0, RoundStatus.Published)], 2)?.scored).toBe(false);
  });

  it('has nothing to join for the first round of a season', () => {
    expect(joinPreview([], 1)).toBeNull();
    expect(joinPreview([r(3)], 2)).toBeNull();
  });
});

describe('ordinalRoundName', () => {
  it('builds Portuguese feminine ordinals', () => {
    expect(ordinalRoundName(1, 'pt-BR')).toBe('Primeira Rodada');
    expect(ordinalRoundName(2, 'pt-BR')).toBe('Segunda Rodada');
    expect(ordinalRoundName(3, 'pt-BR')).toBe('Terceira Rodada');
    expect(ordinalRoundName(10, 'pt-BR')).toBe('Décima Rodada');
    expect(ordinalRoundName(11, 'pt-BR')).toBe('Décima primeira Rodada');
    expect(ordinalRoundName(21, 'pt-BR')).toBe('Vigésima primeira Rodada');
    expect(ordinalRoundName(38, 'pt-BR')).toBe('Trigésima oitava Rodada');
  });

  it('falls back to "Rodada N" beyond the ordinal table', () => {
    expect(ordinalRoundName(150, 'pt-BR')).toBe('Rodada 150');
  });

  it('uses a plain numbered name in English', () => {
    expect(ordinalRoundName(1, 'en-US')).toBe('Round 1');
    expect(ordinalRoundName(7, 'en-US')).toBe('Round 7');
  });
});
