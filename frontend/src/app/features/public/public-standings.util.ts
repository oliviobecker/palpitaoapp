import { ScoreCategory } from '@core/models/enums';
import {
  PublicMatchScore,
  PublicParticipantScore,
  PublicRound,
  PublicRoundSummary,
  PublicStandingRow,
  RoundResultMatch,
} from '@core/models';
import { NumberedRound, compareRounds, roundLabel, sameRound } from '@shared/utils/round-name.util';

/** A standings row with the two gaps a reader actually asks about ("how far off am I?"). */
export interface RankedRow extends PublicStandingRow {
  toLeader: number;
  toAbove: number;
  /** The name right above, unless that row is tied with the leader (then the leader says it all). */
  above: string | null;
}

/**
 * Decorates the standings with each row's distance to the leader and to the row above, then
 * filters by name. The deltas are computed before filtering, so they keep meaning the moment
 * the list is narrowed down to a single name. Filters, never reorders.
 */
export function rankRows(standings: readonly PublicStandingRow[], filter: string): RankedRow[] {
  const leader = standings[0]?.totalPoints ?? 0;
  const ranked = standings.map((row, i) => ({
    ...row,
    toLeader: leader - row.totalPoints,
    toAbove: i === 0 ? 0 : standings[i - 1].totalPoints - row.totalPoints,
    above: i === 0 || standings[i - 1].totalPoints === leader ? null : standings[i - 1].name,
  }));

  const term = filter.trim().toLowerCase();
  return term ? ranked.filter((r) => r.name.toLowerCase().includes(term)) : ranked;
}

/** A participant's line for one match, or null when they did not predict it. */
export function matchScore(
  participant: PublicParticipantScore,
  matchId: string,
): PublicMatchScore | null {
  return participant.matchScores.find((s) => s.roundMatchId === matchId) ?? null;
}

export interface MatchEntry {
  userId: string;
  name: string;
  score: PublicMatchScore;
}

export interface MatchPivotRow {
  match: RoundResultMatch;
  /** Everyone who predicted the match, best first. */
  entries: MatchEntry[];
  /** How many of them scored on it. */
  hits: number;
}

/**
 * The same round, transposed: every participant's line for one match, best first. Pure
 * client-side work — the round payload already carries both sides of the pivot.
 */
export function pivotByMatch(round: PublicRound | null): MatchPivotRow[] {
  if (!round) {
    return [];
  }

  return round.matches.map((match) => {
    const entries = round.participants
      .map((p) => ({ userId: p.userId, name: p.name, score: matchScore(p, match.roundMatchId) }))
      .filter((e): e is MatchEntry => !!e.score)
      .sort(
        (a, b) =>
          b.score.finalPoints - a.score.finalPoints || a.name.localeCompare(b.name, 'pt-BR'),
      );

    return { match, entries, hits: entries.filter((e) => e.score.finalPoints > 0).length };
  });
}

/** A match with no score yet reads as "waiting", never as a missed prediction. */
export function hasResult(match: RoundResultMatch): boolean {
  return match.homeScore != null && match.awayScore != null;
}

/** Translation key of a score category; no category means the prediction missed. */
export function categoryLabelKey(category: ScoreCategory): string {
  return category && category !== ScoreCategory.None
    ? 'category.' + category
    : 'publicStandings.missed';
}

/**
 * "12–14 mai" for a dated round, empty when the season carries no dates. The month is named
 * once when both ends share it — the locale's own day+month format spells out
 * "12 de mai. - 14 de mai.", which is far too long for a dropdown option.
 */
export function formatRoundDates(round: PublicRoundSummary, locale: string): string {
  if (!round.startDate) {
    return '';
  }

  const month = (d: Date) =>
    new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' }).format(d);
  const day = (d: Date) =>
    new Intl.DateTimeFormat(locale, { day: 'numeric', timeZone: 'UTC' }).format(d);

  const start = new Date(round.startDate);
  if (!round.endDate) {
    return `${day(start)} ${month(start)}`;
  }

  const end = new Date(round.endDate);
  if (day(start) === day(end) && month(start) === month(end)) {
    return `${day(start)} ${month(start)}`;
  }
  return month(start) === month(end)
    ? `${day(start)}–${day(end)} ${month(start)}`
    : `${day(start)} ${month(start)} – ${day(end)} ${month(end)}`;
}

/**
 * The round a deep link lands on: the exact round, else whatever now stands at that number — a
 * link shared before the round was split ("10" → 10.1) or merged back ("10.2" → 10) still lands
 * on it — else the newest. `missing` names the round asked for when it is gone, because falling
 * back silently makes the page look like it ignored the link. `available` is newest-first and
 * not empty.
 */
export function resolveRound(
  available: readonly PublicRoundSummary[],
  wanted: NumberedRound | null,
): { round: NumberedRound; missing: string | null } {
  const target = wanted
    ? (available.find((r) => sameRound(r, wanted)) ??
      available.filter((r) => r.number === wanted.number).sort(compareRounds)[0])
    : undefined;
  const pick = target ?? available[0];
  return {
    round: { number: pick.number, part: pick.part ?? 0 },
    missing: !target && wanted ? roundLabel(wanted.number, wanted.part) : null,
  };
}
