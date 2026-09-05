import { describe, expect, it } from 'vitest';
import { formatPublicKey, publicStandingsUrl } from './public-link.util';

describe('formatPublicKey', () => {
  it('groups the key in fours', () => {
    expect(formatPublicKey('A7C39F2E4BD8')).toBe('A7C3-9F2E-4BD8');
  });

  it('leaves a short or empty key alone', () => {
    expect(formatPublicKey('A7C3')).toBe('A7C3');
    expect(formatPublicKey('')).toBe('');
  });
});

describe('publicStandingsUrl', () => {
  it('builds the season link', () => {
    expect(publicStandingsUrl('A7C39F2E4BD8')).toBe(`${window.location.origin}/p/A7C3-9F2E-4BD8`);
  });

  it('deep-links a round when one is given', () => {
    // Round 0 is not a real round, but the guard is on null/undefined, not falsiness.
    expect(publicStandingsUrl('A7C39F2E4BD8', 18)).toContain('?rodada=18');
    expect(publicStandingsUrl('A7C39F2E4BD8', null)).not.toContain('rodada');
  });
});
