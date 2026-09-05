import { describe, expect, it } from 'vitest';
import { avatarColor, initials } from './avatar.util';

describe('initials', () => {
  it('takes the first and last name', () => {
    expect(initials('Joao Silva')).toBe('JS');
    expect(initials('Ana Maria Prado')).toBe('AP');
  });

  it('falls back to a single letter for one-word names', () => {
    expect(initials('Flavio')).toBe('F');
  });

  it('survives padding and empty input', () => {
    expect(initials('   Ze   Carlos  ')).toBe('ZC');
    expect(initials('')).toBe('');
  });
});

describe('avatarColor', () => {
  it('is stable for the same name', () => {
    expect(avatarColor('Ana Prado')).toBe(avatarColor('Ana Prado'));
  });

  it('separates different names', () => {
    expect(avatarColor('Ana Prado')).not.toBe(avatarColor('Flavio Barros'));
  });
});
