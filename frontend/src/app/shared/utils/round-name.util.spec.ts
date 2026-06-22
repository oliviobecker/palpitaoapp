import { describe, expect, it } from 'vitest';
import { ordinalRoundName } from './round-name.util';

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
