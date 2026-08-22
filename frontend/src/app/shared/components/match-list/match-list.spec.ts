import { provideTranslateService } from '@ngx-translate/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Competition, MatchPhase, MatchStatus } from '../../../core/models/enums';
import { RoundMatch } from '../../../core/models/models';
import { MatchList } from './match-list';

function match(partial: Partial<RoundMatch> = {}): RoundMatch {
  return {
    id: 'm1',
    roundId: 'r1',
    competition: Competition.PremierLeague,
    phase: MatchPhase.Regular,
    homeTeamId: 'h',
    homeTeamName: 'Arsenal',
    awayTeamId: 'a',
    awayTeamName: 'Coventry City',
    startsAt: '2026-08-21T19:00:00Z',
    order: 0,
    isFinished: false,
    ...partial,
  };
}

function render(m: RoundMatch): string {
  // No loader: the translate pipe echoes the key back, which is what we assert on.
  TestBed.configureTestingModule({ imports: [MatchList], providers: [provideTranslateService()] });
  const fixture = TestBed.createComponent(MatchList);
  fixture.componentRef.setInput('matches', [m]);
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

describe('MatchList', () => {
  it('marks a live match and shows its current score', () => {
    const text = render(match({ status: MatchStatus.InProgress, homeScore: 3, awayScore: 0 }));

    expect(text).toContain('matchStatus.InProgress');
    expect(text).toContain('3 - 0');
  });

  it('shows neither badge nor score before kickoff', () => {
    const text = render(match({ status: MatchStatus.NotStarted }));

    expect(text).not.toContain('matchStatus.');
    expect(text).not.toContain(' - ');
  });

  it('tolerates a match from an API response that carries no status', () => {
    const text = render(match());

    expect(text).not.toContain('matchStatus.');
    expect(text).toContain('Arsenal');
  });
});
