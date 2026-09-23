import { RoundStatus } from '../../core/models/enums';

// Feminine ordinals (for "rodada") in Portuguese, 1–99 (+100), enough for any
// realistic season. Beyond that we fall back to "Rodada N".
const PT_UNITS = [
  '',
  'primeira',
  'segunda',
  'terceira',
  'quarta',
  'quinta',
  'sexta',
  'sétima',
  'oitava',
  'nona',
];
const PT_TENS = [
  '',
  'décima',
  'vigésima',
  'trigésima',
  'quadragésima',
  'quinquagésima',
  'sexagésima',
  'septuagésima',
  'octogésima',
  'nonagésima',
];

function ptFeminineOrdinal(n: number): string | null {
  if (n >= 1 && n <= 9) return PT_UNITS[n];
  if (n === 100) return 'centésima';
  if (n >= 10 && n <= 99) {
    const tens = PT_TENS[Math.floor(n / 10)];
    const unit = n % 10;
    return unit === 0 ? tens : `${tens} ${PT_UNITS[unit]}`;
  }
  return null;
}

/**
 * Default, pre-filled round title for a given round number, in the active UI
 * language: "Primeira Rodada", "Segunda Rodada", … (pt-BR) / "Round 1" (en-US).
 * The backend twin (`Common/RoundNames.cs`) renames untouched defaults on renumbering.
 */
export function ordinalRoundName(n: number, lang: string): string {
  if (lang.startsWith('pt')) {
    const ordinal = ptFeminineOrdinal(n);
    if (ordinal) {
      return `${ordinal.charAt(0).toUpperCase()}${ordinal.slice(1)} Rodada`;
    }
    return `Rodada ${n}`;
  }
  return `Round ${n}`;
}

/** Anything that carries a round's number and, for a part, its part ("10.2"). */
export interface NumberedRound {
  number: number;
  /** 0 or absent for a standalone round; 1..k for the parts of a round played in parts. */
  part?: number | null;
}

/**
 * How a round is written everywhere — screens, WhatsApp messages, links: "10" for a standalone
 * round, "10.2" for part 2 of round 10 (a week with two lists that counts as one round for
 * absences). Always a dot: the OCR reads x, ×, :, - and – between two digits as a score.
 */
export function roundLabel(number: number, part?: number | null): string {
  return part && part > 0 ? `${number}.${part}` : `${number}`;
}

/**
 * Reads a label back ("10.2" → 10, part 2; "10" → 10, part 0). Split on the dot, never
 * `Number("10.2")`: that would be 10.2, and "10.10" would collapse into 10.1.
 */
export function parseRoundLabel(label: string | null | undefined): NumberedRound | null {
  const match = /^\s*(\d+)(?:\.(\d+))?\s*$/.exec(label ?? '');
  if (!match) {
    return null;
  }
  const number = Number(match[1]);
  const part = match[2] === undefined ? 0 : Number(match[2]);
  return number > 0 ? { number, part } : null;
}

/** Season order: by number, then part. */
export function compareRounds(a: NumberedRound, b: NumberedRound): number {
  return a.number - b.number || (a.part ?? 0) - (b.part ?? 0);
}

/** Same round (number and part), treating a missing part as 0. */
export function sameRound(a: NumberedRound, b: NumberedRound): boolean {
  return compareRounds(a, b) === 0;
}

/**
 * Where a new round lands when created in the previous round's week: the previous round is
 * the highest number below `number` still in play (cancelled rounds are skipped, as the server
 * does), and the new round becomes its next part — a standalone round turns into part 1.
 */
export function joinPreview(
  rounds: readonly (NumberedRound & { status: RoundStatus })[],
  number: number,
): { number: number; part: number; scored: boolean } | null {
  const target = rounds
    .filter((r) => r.number < number && r.status !== RoundStatus.Cancelled)
    .reduce<number | null>((max, r) => (max === null || r.number > max ? r.number : max), null);
  if (target === null) {
    return null;
  }
  const members = rounds.filter((r) => r.number === target);
  return {
    number: target,
    part: members.length + 1,
    scored: members.some((r) => r.status === RoundStatus.Scored),
  };
}
