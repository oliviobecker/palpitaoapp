import { TemporaryStandings } from '../../core/models/models';

/**
 * Builds the WhatsApp-style "partial standings" message for a round: the group
 * name, the round number, one line per participant with the points earned so far
 * and the projected season total, plus the legend and the "it can still change"
 * caveat. Ready to copy — the `*…*` around the group name is WhatsApp's bold
 * syntax, rendered on paste.
 *
 * The rows keep the server's order, which ranks by the points earned *in this
 * round* (see TemporaryStandingsService) — so the number in parentheses is a
 * projected *total*, never a projected position. The legend says as much; do not
 * re-sort here or the two readings get mixed up.
 *
 * A participant heading for an absence gets a “— Ausente” marker. The “pode mudar”
 * caveat below already frames it as provisional, so it needs no extra legend.
 */
export function buildTemporaryStandingsMessage(data: TemporaryStandings, groupTitle = ''): string {
  const lines: string[] = [];
  // Header is the current group/season name (not the product name).
  if (groupTitle.trim()) {
    lines.push(`*${groupTitle.trim()}*`);
  }
  // A backend that predates the roundNumber field leaves it unset; the group still
  // gets a usable heading instead of "Rodada undefined".
  lines.push(data.roundNumber ? `Rodada ${data.roundNumber} — parcial` : 'Parcial da rodada');

  if (data.standings.length === 0) {
    lines.push('');
    lines.push('Ainda sem resultados para a parcial desta rodada.');
    return lines.join('\n');
  }

  lines.push('');
  for (const s of data.standings) {
    // Portuguese literal like the rest of this file: it is WhatsApp content, not UI copy.
    const mark = s.willBeAbsent ? ' — Ausente' : '';
    lines.push(
      `${s.position}. ${s.name}: +${s.roundTemporaryPoints} (${s.projectedTotalPoints})${mark}`,
    );
  }

  lines.push('');
  lines.push('(x) = total projetado no geral');
  lines.push('⏱ Parcial — pode mudar até o fim da rodada.');
  lines.push(progressLine(data));

  return lines.join('\n');
}

/** "17 jogos computados · 6 restantes · atualizado 22/08 11:50" (last part optional). */
function progressLine(data: TemporaryStandings): string {
  const { computedMatches: computed, remainingMatches: remaining } = data;
  const parts = [
    `${computed} ${computed === 1 ? 'jogo computado' : 'jogos computados'}`,
    `${remaining} ${remaining === 1 ? 'restante' : 'restantes'}`,
  ];
  if (data.lastUpdatedAt) {
    parts.push(`atualizado ${formatUpdatedAt(data.lastUpdatedAt)}`);
  }
  return parts.join(' · ');
}

/** "22/08 11:50" from an ISO timestamp, in local time — the same format the page shows. */
function formatUpdatedAt(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => `${n}`.padStart(2, '0');
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
