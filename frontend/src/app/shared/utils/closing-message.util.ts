import { Competition, MatchPhase } from '../../core/models/enums';
import { RoundResultMatch, RoundResults, Standing } from '../../core/models/models';

const COMP_LABEL: Record<Competition, string> = {
  [Competition.PremierLeague]: 'Premier League',
  [Competition.Championship]: 'Championship',
  [Competition.FACup]: 'FA Cup',
  [Competition.LeagueOne]: 'League One',
  [Competition.FifaWorldCup]: 'Copa do Mundo FIFA',
};

// Feminine number words for the absence legend ("Uma ausência", "Duas ausências"…).
const ABSENCE_WORDS = ['', 'Uma', 'Duas', 'Três', 'Quatro', 'Cinco', 'Seis', 'Sete', 'Oito'];

/**
 * Builds the WhatsApp-style "round closed" message: the final scores grouped by
 * relevance (knockouts first, then the regular block, then League One), the
 * points earned in the round per participant and the overall ranking with
 * absence markers and eliminations. Plain text, ready to copy.
 */
export function buildClosingMessage(
  roundNumber: number,
  results: RoundResults,
  standings: Standing[],
  groupTitle = '',
): string {
  const lines: string[] = [];
  // Header is the current group/season name (not the product name).
  if (groupTitle.trim()) {
    lines.push(groupTitle.trim());
  }
  lines.push(`Rodada ${roundNumber}`);
  lines.push('');
  lines.push('Encerrados:');

  const finished = results.matches.filter((m) => m.homeScore != null && m.awayScore != null);
  const footnotes: string[] = [];

  // 1) Knockout / non-regular matches: each as its own labeled block.
  for (const m of finished.filter((m) => m.phase !== MatchPhase.Regular)) {
    lines.push('');
    lines.push(`${phaseHeader(m.phase)} ${COMP_LABEL[m.competition]} ×${m.multiplier}`.trim());
    lines.push(scoreLine(m));
  }

  // 2) Regular matches, excluding League One (kept in its own block below).
  const regular = finished.filter(
    (m) => m.phase === MatchPhase.Regular && m.competition !== Competition.LeagueOne,
  );
  if (regular.length > 0) {
    lines.push('');
    for (const m of regular) {
      if (m.multiplier > 1) {
        if (!footnotes.some((f) => f.startsWith('✲✲'))) {
          footnotes.push(`✲✲ Clássico: ×${m.multiplier}.`);
        }
        lines.push(`${scoreLine(m)} ✲✲`);
      } else {
        lines.push(scoreLine(m));
      }
    }
  }

  // 3) League One block (always flagged with ✲ and explained in a footnote).
  const leagueOne = finished.filter(
    (m) => m.phase === MatchPhase.Regular && m.competition === Competition.LeagueOne,
  );
  if (leagueOne.length > 0) {
    lines.push('');
    for (const m of leagueOne) {
      if (!footnotes.some((f) => f.startsWith('✲ League One'))) {
        footnotes.push(`✲ League One: ×${m.multiplier}.`);
      }
      lines.push(`${scoreLine(m)} ✲`);
    }
  }

  if (footnotes.length > 0) {
    lines.push('');
    lines.push(...footnotes);
  }

  // --- Pontuação: points earned this round, ties grouped on one line ---------
  const players = results.participants
    .filter((p) => !p.wasAbsent)
    .sort((a, b) => b.finalPoints - a.finalPoints || a.name.localeCompare(b.name, 'pt-BR'));
  if (players.length > 0) {
    lines.push('');
    lines.push(`Pontuação ${roundNumber}`);
    lines.push('');
    let i = 0;
    while (i < players.length) {
      const pts = players[i].finalPoints;
      const group: string[] = [];
      while (i < players.length && players[i].finalPoints === pts) {
        group.push(players[i].name);
        i++;
      }
      lines.push(`${joinNames(group)}: ${formatPoints(pts)}`);
    }
  }

  // --- Rank: overall standings with absence markers and eliminations ---------
  const ranked = [...standings].sort((a, b) => a.position - b.position);
  if (ranked.length > 0) {
    lines.push('');
    lines.push('——');
    lines.push('');
    lines.push(`Rank ${roundNumber}`);
    lines.push('');
    for (const s of ranked) {
      const marks = '*'.repeat(s.isEliminated ? 0 : s.absenceCount);
      const value = s.isEliminated ? 'Eliminado' : formatPoints(s.totalPoints);
      lines.push(`${s.position}. ${s.name}${marks}: ${value}`);
    }

    const maxAbsence = Math.max(
      0,
      ...ranked.filter((s) => !s.isEliminated).map((s) => s.absenceCount),
    );
    if (maxAbsence > 0) {
      lines.push('');
      for (let n = 1; n <= maxAbsence; n++) {
        const word = ABSENCE_WORDS[n] ?? `${n}`;
        lines.push(`${'*'.repeat(n)} ${word} ausência${n > 1 ? 's' : ''}`);
      }
    }
  }

  return lines.join('\n');
}

function scoreLine(m: RoundResultMatch): string {
  return `${m.homeTeamName} ${m.homeScore} x ${m.awayScore} ${m.awayTeamName}`;
}

function phaseHeader(phase: MatchPhase): string {
  switch (phase) {
    case MatchPhase.PlayoffSemiFinal:
    case MatchPhase.FACupSemiFinal:
      return 'SEMIFINAL';
    case MatchPhase.PlayoffFinal:
    case MatchPhase.FACupFinal:
      return 'FINAL';
    default:
      return 'JOGO';
  }
}

/** "A", "A e B", "A, B e C" (Portuguese list joining). */
function joinNames(names: string[]): string {
  if (names.length <= 1) return names[0] ?? '';
  return `${names.slice(0, -1).join(', ')} e ${names[names.length - 1]}`;
}

/** Integer as-is; fractional points use the Brazilian decimal comma (e.g. 476,5). */
function formatPoints(points: number): string {
  return String(points).replace('.', ',');
}
