import { ManualPredictionItem, RoundMatch } from '@core/models';

/** One match's pair of score boxes, as the form holds them. */
export interface ScoreEntry {
  home: unknown;
  away: unknown;
}

/** The number typed in a score box, or null while it is empty — an empty box is never 0. */
export function typedScore(value: unknown): number | null {
  if (value === null || value === undefined || (typeof value === 'string' && !value.trim())) {
    return null;
  }
  const score = Number(value);
  return Number.isFinite(score) ? score : null;
}

/** How many matches still have an empty score box, on either side. */
export function missingScoreCount(entries: readonly ScoreEntry[]): number {
  return entries.filter((e) => typedScore(e.home) === null || typedScore(e.away) === null).length;
}

/**
 * The request rows for every match, or null while any score box is empty. An untyped box can
 * never reach the API as 0: that is how a line the OCR import missed was saved as a real 0x0.
 */
export function manualPredictionItems(
  matches: readonly Pick<RoundMatch, 'id'>[],
  entries: readonly ScoreEntry[],
): ManualPredictionItem[] | null {
  const items: ManualPredictionItem[] = [];
  for (const [i, match] of matches.entries()) {
    const home = typedScore(entries[i]?.home);
    const away = typedScore(entries[i]?.away);
    if (home === null || away === null) {
      return null;
    }
    items.push({ roundMatchId: match.id, predictedHomeScore: home, predictedAwayScore: away });
  }
  return items;
}
