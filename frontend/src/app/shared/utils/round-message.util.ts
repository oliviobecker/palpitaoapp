import { Competition } from '../../core/models/enums';
import { Round } from '../../core/models/models';
import { predictionDeadlineIso } from './deadline.util';
import { computeMultiplier, phaseLabel } from './match.util';
import { shortTeamName } from './team-name.util';

const COMP_LABEL: Record<Competition, string> = {
  [Competition.PremierLeague]: 'Premier League',
  [Competition.Championship]: 'Championship',
  [Competition.FACup]: 'FA Cup',
  [Competition.LeagueOne]: 'League One',
  [Competition.FifaWorldCup]: 'Copa do Mundo FIFA',
};

// Order the competitions appear in the group message.
const COMP_ORDER: Competition[] = [
  Competition.PremierLeague,
  Competition.Championship,
  Competition.FACup,
  Competition.LeagueOne,
  Competition.FifaWorldCup,
];

/**
 * Builds the WhatsApp-style group message for a round: title, round number, the
 * prediction deadline (kickoff of the earliest match) and the matches grouped by
 * competition with their multipliers/phases annotated. Ready to copy — the `*…*`
 * around the headings is WhatsApp's bold syntax, rendered on paste.
 */
export function buildRoundMessage(round: Round, groupTitle = ''): string {
  const lines: string[] = [];
  // Header is the current group/season name (not the product name).
  if (groupTitle.trim()) {
    lines.push(`*${groupTitle.trim()}*`);
  }
  lines.push(`${round.title ? round.title + ', ' : ''}Rodada ${round.number}`);

  const matches = [...(round.matches ?? [])];
  if (matches.length === 0) {
    lines.push('');
    lines.push('Sem jogos cadastrados ainda.');
    return lines.join('\n');
  }

  // The deadline is one minute before the first kickoff, not the kickoff itself.
  const deadline = predictionDeadlineIso(round);
  lines.push('');
  if (deadline) {
    lines.push(`Palpites até ${formatDeadline(deadline)}:`);
  }

  // Flávio rule (round 16+): the leader has a special, earlier deadline.
  const flavio = round.flavio;
  if (flavio?.applies && flavio.leaderNames.length > 0 && flavio.deadlineUtc) {
    const mentions = flavio.leaderNames.map((n) => `@${n}`).join(', ');
    const verb = flavio.leaderNames.length === 1 ? 'tem' : 'têm';
    // The group is told the rule's window ("24 horas"). When the general lock cuts that
    // window short the count would promise time the leader does not have, so the exact
    // instant takes over.
    const limit =
      flavio.windowHours && !flavio.deadlineCappedByLock
        ? `${flavio.windowHours} horas`
        : formatDeadline(flavio.deadlineUtc);
    lines.push('');
    lines.push(`*REGRA FLÁVIO:* ${mentions} ${verb} até ${limit} para palpitar`);
  }

  const multipliers = new Set<number>();
  for (const comp of COMP_ORDER) {
    const group = matches
      .filter((m) => m.competition === comp)
      .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
    if (group.length === 0) continue;

    lines.push('');
    lines.push(`*${COMP_LABEL[comp]}*`);
    for (const m of group) {
      const mult = computeMultiplier(m);
      multipliers.add(mult);
      const tags: string[] = [];
      const phase = phaseLabel(m.phase);
      if (phase) tags.push(phase);
      if (mult > 1) tags.push(`×${mult}`);
      const suffix = tags.length ? ` (${tags.join(' ')})` : '';
      // Short names are for display only — `computeMultiplier` above still sees the
      // full name, which is what `isBigSeven` matches on.
      lines.push(`${shortTeamName(m.homeTeamName)} x ${shortTeamName(m.awayTeamName)}${suffix}`);
    }
  }

  if (multipliers.size > 1 || multipliers.has(2) || multipliers.has(3)) {
    lines.push('');
    lines.push('Regras: ×2/×3 = multiplicador do jogo (clássicos, League One, mata-mata).');
  }

  return lines.join('\n');
}

/** "11h29 de sábado (23/05/2026)" from an ISO timestamp, in local time. */
function formatDeadline(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => `${n}`.padStart(2, '0');
  const time = `${d.getHours()}h${pad(d.getMinutes())}`;
  const weekday = new Intl.DateTimeFormat('pt-BR', { weekday: 'long' }).format(d);
  const date = `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
  return `${time} de ${weekday} (${date})`;
}
