import { OcrParticipantAlias } from '@core/models';

/** Filters by the name as it was read and by the participant it points at. */
export function filterAliases(
  aliases: OcrParticipantAlias[],
  query: string,
): OcrParticipantAlias[] {
  const term = query.trim().toLowerCase();
  if (!term) {
    return aliases;
  }

  return aliases.filter(
    (a) =>
      a.aliasRaw.toLowerCase().includes(term) ||
      a.alias.includes(term) ||
      a.userName.toLowerCase().includes(term),
  );
}
