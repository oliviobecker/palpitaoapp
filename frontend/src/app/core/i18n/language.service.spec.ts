import { describe, expect, it } from 'vitest';
import { pickLanguage } from './language.service';

describe('pickLanguage', () => {
  it('uses pt-BR when the browser is Portuguese', () => {
    expect(pickLanguage('pt-BR', null)).toBe('pt-BR');
    expect(pickLanguage('pt', null)).toBe('pt-BR');
  });

  it('uses en-US when the browser is English', () => {
    expect(pickLanguage('en-US', null)).toBe('en-US');
    expect(pickLanguage('en', null)).toBe('en-US');
  });

  it('falls back to en-US for other languages', () => {
    expect(pickLanguage('fr-FR', null)).toBe('en-US');
    expect(pickLanguage(undefined, null)).toBe('en-US');
  });

  it('honours a saved preference over the browser', () => {
    expect(pickLanguage('en-US', 'pt-BR')).toBe('pt-BR');
    expect(pickLanguage('pt-BR', 'en-US')).toBe('en-US');
  });
});
