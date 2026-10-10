import { describe, expect, it } from 'vitest';
import { OcrParticipantAlias } from '@core/models/models';
import { filterAliases } from './admin-ocr-aliases';

function alias(aliasRaw: string, userName: string): OcrParticipantAlias {
  return {
    id: crypto.randomUUID(),
    alias: aliasRaw.toLowerCase(),
    aliasRaw,
    userId: crypto.randomUUID(),
    userName,
    createdAt: '2026-08-15T12:00:00Z',
    updatedAt: '2026-08-15T12:00:00Z',
  };
}

describe('filterAliases', () => {
  const list = [alias('Careca', 'TB'), alias('nAc', 'Flavio'), alias('Pacheco', 'Thiaggo')];

  it('returns everything for an empty query', () => {
    expect(filterAliases(list, '   ')).toHaveLength(3);
  });

  it('matches the alias as it was read', () => {
    expect(filterAliases(list, 'carec').map((a) => a.aliasRaw)).toEqual(['Careca']);
  });

  it('matches the participant it points at', () => {
    // The admin looking for "everything filed against Flavio" is the common case.
    expect(filterAliases(list, 'flavio').map((a) => a.aliasRaw)).toEqual(['nAc']);
  });

  it('ignores casing on both sides', () => {
    expect(filterAliases(list, 'NAC').map((a) => a.aliasRaw)).toEqual(['nAc']);
  });

  it('returns nothing when neither side matches', () => {
    expect(filterAliases(list, 'zzz')).toEqual([]);
  });
});
