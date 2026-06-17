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
