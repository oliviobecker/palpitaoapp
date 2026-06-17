import { RoundScout, ScoutMatch } from '../../core/models/models';

/**
 * Builds the WhatsApp-style "Scout" block for a single match: a header plus the
 * participants grouped by the exact scoreline they predicted. Plain text, ready
 * to copy. Example:
 *
 *   Scout Man United x Man City
 *
 *   - 1x1 @Felipe
 *   - 2x0 @Bruno @Dourado
 */
export function buildMatchScoutMessage(match: ScoutMatch): string {
  const lines: string[] = [`Scout ${match.homeTeamName} x ${match.awayTeamName}`, ''];
  if (match.groups.length === 0) {
    lines.push('- (sem palpites)');
  } else {
    for (const g of match.groups) {
      const mentions = g.names.map((n) => `@${n}`).join(' ');
      lines.push(`- ${g.homeScore}x${g.awayScore} ${mentions}`);
    }
  }
  return lines.join('\n');
}

/**
 * Builds the full round Scout message: one {@link buildMatchScoutMessage} block
 * per match, separated by a blank line.
 */
export function buildScoutMessage(scout: RoundScout): string {
  return scout.matches.map(buildMatchScoutMessage).join('\n\n');
}
