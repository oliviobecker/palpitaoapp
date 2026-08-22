import { describe, expect, it } from 'vitest';
import { RoundStatus } from '../../core/models/enums';
import { TemporaryStanding, TemporaryStandings } from '../../core/models/models';
import { buildTemporaryStandingsMessage } from './temporary-standings-message.util';

function row(partial: Partial<TemporaryStanding>): TemporaryStanding {
  return {
    position: 1,
    userId: 'u1',
    name: 'João',
    roundTemporaryPoints: 0,
    currentOfficialTotalPoints: 0,
    projectedTotalPoints: 0,
    computedMatches: 17,
    remainingMatches: 6,
    willBeAbsent: false,
    ...partial,
  };
}

function standings(partial: Partial<TemporaryStandings> = {}): TemporaryStandings {
  return {
    roundId: 'r1',
    roundNumber: 12,
    isTemporary: true,
    roundStatus: RoundStatus.Published,
    lastUpdatedAt: '2026-08-22T14:50:00Z',
    computedMatches: 17,
    remainingMatches: 6,
    standings: [
      row({
        position: 1,
        userId: 'u1',
        name: 'Bruno Vilaça',
        roundTemporaryPoints: 15,
        projectedTotalPoints: 476,
      }),
      row({
        position: 2,
        userId: 'u2',
        name: 'Edson',
        roundTemporaryPoints: 11,
        projectedTotalPoints: 472,
      }),
      row({
        position: 3,
        userId: 'u3',
        name: 'Gilberto',
        roundTemporaryPoints: 11,
        projectedTotalPoints: 468,
      }),
    ],
    ...partial,
  };
}

describe('buildTemporaryStandingsMessage', () => {
  it('leads with the group name in bold and the round number', () => {
    const text = buildTemporaryStandingsMessage(standings(), 'Palpitão England');
    expect(text.startsWith('*Palpitão England*\nRodada 12 — parcial')).toBe(true);
  });

  it('omits the group line when there is no group title', () => {
    const text = buildTemporaryStandingsMessage(standings(), '   ');
    expect(text.startsWith('Rodada 12 — parcial')).toBe(true);
    expect(text).not.toContain('*');
  });

  it('falls back to a generic heading when the round number is missing', () => {
    // A backend that predates the roundNumber field.
    const text = buildTemporaryStandingsMessage(standings({ roundNumber: 0 }));
    expect(text).toContain('Parcial da rodada');
    expect(text).not.toContain('Rodada 0');
  });

  it('prints one line per participant with the round points and the projected total', () => {
    const text = buildTemporaryStandingsMessage(standings());
    expect(text).toContain('1. Bruno Vilaça: +15 (476)');
    expect(text).toContain('2. Edson: +11 (472)');
    expect(text).toContain('3. Gilberto: +11 (468)');
  });

  it('keeps the server order even when the projected totals disagree with it', () => {
    // The server ranks by the points earned in the round, so a veteran sitting on a
    // big official total can trail someone who scored more this round. Re-sorting
    // here would silently rewrite the ranking the page shows.
    const text = buildTemporaryStandingsMessage(
      standings({
        standings: [
          row({
            position: 1,
            userId: 'u1',
            name: 'Novato',
            roundTemporaryPoints: 15,
            projectedTotalPoints: 90,
          }),
          row({
            position: 2,
            userId: 'u2',
            name: 'Veterano',
            roundTemporaryPoints: 4,
            projectedTotalPoints: 512,
          }),
        ],
      }),
    );
    expect(text).toMatch(/1\. Novato: \+15 \(90\)[\s\S]*2\. Veterano: \+4 \(512\)/);
  });

  it('explains the parenthesised number and warns the standings are partial', () => {
    const text = buildTemporaryStandingsMessage(standings());
    expect(text).toContain('(x) = total projetado no geral');
    expect(text).toContain('⏱ Parcial — pode mudar até o fim da rodada.');
  });

  it('reports the match counts, in the plural', () => {
    const text = buildTemporaryStandingsMessage(standings());
    expect(text).toContain('17 jogos computados · 6 restantes');
  });

  it('reports the match counts in the singular when there is one of each', () => {
    const text = buildTemporaryStandingsMessage(
      standings({ computedMatches: 1, remainingMatches: 1 }),
    );
    expect(text).toContain('1 jogo computado · 1 restante');
  });

  it('appends the update time in the page format', () => {
    const iso = '2026-08-22T14:50:00Z';
    // Local time, like the DatePipe on the page — never hard-code the wall clock.
    const d = new Date(iso);
    const pad = (n: number) => `${n}`.padStart(2, '0');
    const expected = `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    expect(buildTemporaryStandingsMessage(standings({ lastUpdatedAt: iso }))).toContain(
      `atualizado ${expected}`,
    );
  });

  it('omits the update time when the round was never refreshed', () => {
    const text = buildTemporaryStandingsMessage(standings({ lastUpdatedAt: null }));
    expect(text).not.toContain('atualizado');
    expect(text).toContain('17 jogos computados · 6 restantes');
  });

  it('marks whoever is heading for an absence, leaving the other lines untouched', () => {
    const text = buildTemporaryStandingsMessage(
      standings({
        standings: [
          row({
            position: 1,
            userId: 'u1',
            name: 'Bruno Vilaça',
            roundTemporaryPoints: 15,
            projectedTotalPoints: 476,
          }),
          row({ position: 2, userId: 'u9', name: 'João Paulo', willBeAbsent: true }),
        ],
      }),
    );

    expect(text).toContain('2. João Paulo: +0 (0) — Ausente');
    // The format the group already knows must not shift for everyone else.
    expect(text).toContain('1. Bruno Vilaça: +15 (476)');
    expect(text).not.toContain('1. Bruno Vilaça: +15 (476) —');
  });

  it('says so when there is nothing to rank yet', () => {
    const text = buildTemporaryStandingsMessage(standings({ standings: [] }), 'Palpitão England');
    expect(text).toContain('Ainda sem resultados para a parcial desta rodada.');
    expect(text).not.toContain('(x) =');
  });
});
