import { describe, expect, it } from 'vitest';
import { TEAM_SHORT_NAMES, shortTeamName } from './team-name.util';

describe('shortTeamName', () => {
  it('shortens the clubs in the table', () => {
    expect(shortTeamName('Wolverhampton Wanderers')).toBe('Wolves');
    expect(shortTeamName('Queens Park Rangers')).toBe('QPR');
    expect(shortTeamName('Sheffield United')).toBe('Sheffield Utd');
    expect(shortTeamName('Sheffield Wednesday')).toBe('Sheffield Wed');
    expect(shortTeamName('Manchester United')).toBe('Man Utd');
    expect(shortTeamName('West Bromwich Albion')).toBe('West Brom');
    expect(shortTeamName('AFC Wimbledon')).toBe('Wimbledon');
    expect(shortTeamName('Brighton & Hove Albion')).toBe('Brighton');
  });

  it('passes unknown names through verbatim', () => {
    // National teams and clubs auto-created by the fixture import are never in the
    // table; mangling them would break the OCR round trip.
    for (const name of ['Bélgica', 'Nova Zelândia', 'Arábia Saudita', 'RD Congo', 'Brazil']) {
      expect(shortTeamName(name)).toBe(name);
    }
  });

  it('tolerates stray case and whitespace', () => {
    expect(shortTeamName('  manchester   united ')).toBe('Man Utd');
  });

  it('keeps clubs that are already short as they are', () => {
    for (const name of ['Arsenal', 'Tottenham', 'Crystal Palace', 'Nottingham Forest']) {
      expect(shortTeamName(name)).toBe(name);
    }
  });

  it('lists exactly the short names that need a backend OCR alias', () => {
    // OcrTeamMatcher resolves a short name for free when it is a substring of the full
    // name. The rest need a row in Services/Ocr/OcrTeamMatcher.TeamAliases and in
    // OcrShortNameRoundTripTests, or the screenshot of the message stops importing.
    const needsAlias = Object.entries(TEAM_SHORT_NAMES)
      .filter(([full, short]) => !full.toLowerCase().includes(short.toLowerCase()))
      .map(([, short]) => short)
      .sort();

    expect(needsAlias).toEqual(['Man City', 'Man Utd', 'QPR', 'Sheffield Utd', 'Wolves']);
  });

  it('never maps two clubs onto the same short name', () => {
    const shortNames = Object.values(TEAM_SHORT_NAMES);
    expect(new Set(shortNames).size).toBe(shortNames.length);
  });

  it('is idempotent, so a short name is never shortened again', () => {
    for (const full of Object.keys(TEAM_SHORT_NAMES)) {
      const short = shortTeamName(full);
      expect(shortTeamName(short)).toBe(short);
    }
  });
});
