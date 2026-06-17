import { describe, expect, it } from 'vitest';
import { RoundScout } from '../../core/models/models';
import { buildScoutMessage } from './scout-message.util';

const scout: RoundScout = {
  roundId: 'r1',
  roundNumber: 1,
  roundTitle: 'Primeira Rodada',
  matches: [
    {
      roundMatchId: 'm1',
      homeTeamName: 'Man United',
      awayTeamName: 'Man City',
      groups: [
        { homeScore: 1, awayScore: 1, names: ['Felipe'] },
        { homeScore: 2, awayScore: 0, names: ['Bruno', 'Dourado'] },
      ],
    },
  ],
};

describe('buildScoutMessage', () => {
  it('renders a header and one bullet per scoreline with @mentions', () => {
    const text = buildScoutMessage(scout);
    expect(text).toContain('Scout Man United x Man City');
    expect(text).toContain('- 1x1 @Felipe');
    expect(text).toContain('- 2x0 @Bruno @Dourado');
  });

  it('separates multiple matches with a blank line', () => {
    const two: RoundScout = {
      ...scout,
      matches: [
        scout.matches[0],
        { roundMatchId: 'm2', homeTeamName: 'Arsenal', awayTeamName: 'Chelsea', groups: [] },
      ],
    };
    const text = buildScoutMessage(two);
    expect(text).toContain('Scout Arsenal x Chelsea');
    expect(text).toMatch(/Scout Man United x Man City[\s\S]*\n\nScout Arsenal x Chelsea/);
  });

  it('shows a placeholder when a match has no predictions', () => {
    const empty: RoundScout = {
      ...scout,
      matches: [{ roundMatchId: 'm1', homeTeamName: 'A', awayTeamName: 'B', groups: [] }],
    };
    expect(buildScoutMessage(empty)).toContain('- (sem palpites)');
  });
});
