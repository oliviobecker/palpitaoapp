import { describe, expect, it } from 'vitest';
import { Competition, MatchPhase, RoundStatus } from '../../core/models/enums';
import { Round, RoundMatch } from '../../core/models/models';
import { buildRoundMessage } from './round-message.util';

function match(partial: Partial<RoundMatch>): RoundMatch {
  return {
    id: partial.id ?? 'm',
    roundId: 'r1',
    competition: partial.competition ?? Competition.PremierLeague,
    phase: partial.phase ?? MatchPhase.Regular,
    homeTeamId: 'h',
    homeTeamName: partial.homeTeamName ?? 'Home',
    awayTeamId: 'a',
    awayTeamName: partial.awayTeamName ?? 'Away',
    startsAt: partial.startsAt ?? '2026-05-23T13:30:00Z',
    order: partial.order ?? 0,
    isFinished: false,
    manualMultiplierOverride: partial.manualMultiplierOverride ?? null,
  };
}

function round(matches: RoundMatch[], extra: Partial<Round> = {}): Round {
  return {
    id: 'r1',
    seasonId: 's1',
    number: 41,
    title: null,
    status: RoundStatus.Draft,
    createdAt: '2026-01-01T00:00:00Z',
    matches,
    ...extra,
  };
}

describe('buildRoundMessage', () => {
  it('renders title, round number, deadline and grouped matches with multipliers', () => {
    const msg = buildRoundMessage(
      round([
        match({
          id: '1',
          competition: Competition.PremierLeague,
          homeTeamName: 'Arsenal',
          awayTeamName: 'Chelsea',
          startsAt: '2026-05-23T13:30:00Z',
        }),
        match({
          id: '2',
          competition: Competition.Championship,
          phase: MatchPhase.PlayoffFinal,
          homeTeamName: 'Hull',
          awayTeamName: 'Middlesbrough',
          startsAt: '2026-05-24T15:00:00Z',
        }),
        match({
          id: '3',
          competition: Competition.LeagueOne,
          homeTeamName: 'Bolton',
          awayTeamName: 'Stockport',
          startsAt: '2026-05-24T12:00:00Z',
        }),
      ]),
      'Palpitão England 2025/2026',
    );

    // The header is the group/season name passed in (an example group, not the product).
    expect(msg).toContain('Palpitão England 2025/2026');
    expect(msg).toContain('Rodada 41');
    expect(msg).toMatch(/Palpites até \d{1,2}h\d{2} de \p{L}+ \(\d{2}\/\d{2}\/\d{4}\):/u);

    expect(msg).toContain('Premier League');
    expect(msg).toContain('Arsenal x Chelsea (×2)'); // Big Seven derby
    expect(msg).toContain('Championship');
    expect(msg).toContain('Hull x Middlesbrough (Final (Playoff) ×2)');
    expect(msg).toContain('League One');
    expect(msg).toContain('Bolton x Stockport (×2)');
    expect(msg).toContain('Regras:');
  });

  it('honours a manual multiplier override', () => {
    const msg = buildRoundMessage(
      round([
        match({
          competition: Competition.FACup,
          phase: MatchPhase.FACupFinal,
          homeTeamName: 'Chelsea',
          awayTeamName: 'Manchester City',
          manualMultiplierOverride: 3,
        }),
      ]),
    );
    expect(msg).toContain('Chelsea x Manchester City (Final ×3)');
  });

  it('adds the Flávio leader line when the rule applies', () => {
    const msg = buildRoundMessage(
      round([match({ homeTeamName: 'Arsenal', awayTeamName: 'Chelsea' })], {
        flavio: {
          applies: true,
          leaderNames: ['Manoel Neto'],
          deadlineUtc: '2026-05-22T23:59:00Z',
        },
      }),
    );
    expect(msg).toMatch(/Líder @Manoel Neto tem até .+ para palpitar\./);
  });

  it('omits the Flávio line when there is no leader or no deadline', () => {
    const noLeader = buildRoundMessage(
      round([match({})], { flavio: { applies: true, leaderNames: [], deadlineUtc: null } }),
    );
    expect(noLeader).not.toContain('para palpitar.');

    const noFlavio = buildRoundMessage(round([match({})]));
    expect(noFlavio).not.toContain('para palpitar.');
  });

  it('shows a placeholder when the round has no matches', () => {
    const msg = buildRoundMessage(round([]));
    expect(msg).toContain('Rodada 41');
    expect(msg).toContain('Sem jogos cadastrados ainda.');
    expect(msg).not.toContain('Palpites até');
  });
});
