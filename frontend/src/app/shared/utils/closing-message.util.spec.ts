import { describe, expect, it } from 'vitest';
import { Competition, MatchPhase, RoundStatus } from '../../core/models/enums';
import { RoundResults, Standing } from '../../core/models/models';
import { buildClosingMessage } from './closing-message.util';

function match(partial: Partial<RoundResults['matches'][number]>) {
  return {
    roundMatchId: 'm',
    competition: Competition.PremierLeague,
    phase: MatchPhase.Regular,
    homeTeamName: 'Home',
    awayTeamName: 'Away',
    homeScore: 1,
    awayScore: 0,
    isFinished: true,
    multiplier: 1,
    isClassic: false,
    isManualMultiplier: false,
    ...partial,
  };
}

function participant(name: string, finalPoints: number, wasAbsent = false) {
  return {
    userId: name,
    name,
    grossPoints: finalPoints,
    finalPoints,
    penaltyPoints: 0,
    wasAbsent,
    wasEliminated: false,
    flavioRuleApplied: false,
    matchScores: [],
  };
}

const results: RoundResults = {
  roundId: 'r1',
  status: RoundStatus.Scored,
  matches: [
    match({
      competition: Competition.Championship,
      phase: MatchPhase.PlayoffFinal,
      homeTeamName: 'Hull',
      awayTeamName: 'Middlesbrough',
      homeScore: 1,
      awayScore: 0,
      multiplier: 2,
    }),
    match({ homeTeamName: 'Tottenham', awayTeamName: 'Everton', homeScore: 1, awayScore: 0 }),
    match({
      homeTeamName: 'Wolverhampton Wanderers',
      awayTeamName: 'Queens Park Rangers',
      homeScore: 2,
      awayScore: 1,
    }),
    match({
      competition: Competition.LeagueOne,
      homeTeamName: 'Bolton',
      awayTeamName: 'Stockport',
      homeScore: 4,
      awayScore: 1,
      multiplier: 2,
    }),
  ],
  participants: [
    participant('Edson', 18),
    participant('Becker', 13),
    participant('DeFarias', 3),
    participant('Vilaça', 3),
    participant('Faltoso', 0, true),
  ],
};

const standings: Standing[] = [
  {
    position: 1,
    userId: 'PL',
    name: 'PL',
    totalPoints: 478,
    playedRounds: 41,
    absenceCount: 0,
    penaltyPoints: 0,
    isEliminated: false,
  },
  {
    position: 2,
    userId: 'Edson',
    name: 'Edson',
    totalPoints: 440,
    playedRounds: 40,
    absenceCount: 1,
    penaltyPoints: 0,
    isEliminated: false,
  },
  {
    position: 3,
    userId: 'Flávio',
    name: 'Flávio',
    totalPoints: 0,
    playedRounds: 10,
    absenceCount: 0,
    penaltyPoints: 0,
    isEliminated: true,
  },
];

describe('buildClosingMessage', () => {
  const text = buildClosingMessage(41, results, standings, 'Palpitão England 2025/2026');

  it('renders the header and the Encerrados section', () => {
    // The header is the group/season name passed in (an example group, not the product).
    expect(text).toContain('*Palpitão England 2025/2026*');
    expect(text).toContain('Rodada 41');
    expect(text).toContain('*Encerrados:*');
  });

  it('puts knockout matches in their own labelled block', () => {
    expect(text).toContain('*FINAL Championship ×2*');
    expect(text).toContain('Hull 1 x 0 Middlesbrough');
  });

  it('prints the short team names in the score lines', () => {
    expect(text).toContain('Wolves 2 x 1 QPR');
  });

  it('flags League One with a footnote', () => {
    expect(text).toContain('Bolton 4 x 1 Stockport ✲');
    expect(text).toContain('✲ League One: ×2.');
  });

  it('lists round points and groups tied participants with "e", skipping absentees', () => {
    expect(text).toContain('*Pontuação 41*');
    expect(text).toContain('Edson: 18');
    expect(text).toContain('DeFarias e Vilaça: 3');
    expect(text).not.toContain('Faltoso');
  });

  it('renders the ranking with absence markers and eliminations', () => {
    expect(text).toContain('*Rank 41*');
    expect(text).toContain('1. PL: 478');
    // The marker is ✱, not "*": a stray asterisk would pair with the bold delimiters.
    expect(text).toContain('2. Edson✱: 440');
    expect(text).toContain('3. Flávio: Eliminado');
    expect(text).toContain('✱ Uma ausência');
  });
});

describe('buildClosingMessage — public link', () => {
  it('omits the link block when no URL is given', () => {
    const text = buildClosingMessage(41, results, standings, 'Palpitão');
    expect(text).not.toContain('Confira a pontuação jogo a jogo');
  });

  it('appends the link as the last line, bare', () => {
    const url = 'https://palpitao.app/p/A7C3-9F2E-4BD8?rodada=41';
    const text = buildClosingMessage(41, results, standings, 'Palpitão', url);

    expect(text).toContain('Confira a pontuação jogo a jogo:');
    // Bare and last: WhatsApp only autolinks a URL that no bold delimiter touches.
    expect(text.trimEnd().endsWith(url)).toBe(true);
    expect(text).not.toContain(`*${url}*`);
  });
});
