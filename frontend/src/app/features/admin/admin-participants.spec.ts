import { describe, expect, it } from 'vitest';
import { RoundStatus } from '../../core/models/enums';
import { AbsenceCandidateRound } from '../../core/models/models';
import { isPreselectedAbsence } from './admin-participants';

function candidate(partial: Partial<AbsenceCandidateRound> = {}): AbsenceCandidateRound {
  return {
    roundId: 'r1',
    number: 1,
    title: null,
    status: RoundStatus.Locked,
    matchCount: 10,
    predictionCount: 0,
    requiresRescore: false,
    hasPresentOverride: false,
    ...partial,
  };
}

describe('isPreselectedAbsence', () => {
  it('ticks a locked round — its absence lands on its own when the round is scored', () => {
    expect(isPreselectedAbsence(candidate())).toBe(true);
  });

  it('leaves a scored round unticked, since it only counts after a deliberate re-score', () => {
    expect(
      isPreselectedAbsence(candidate({ status: RoundStatus.Scored, requiresRescore: true })),
    ).toBe(false);
  });

  it('leaves a round the participant was excused from unticked', () => {
    // Silently reversing an explicit "present" override would undo an admin decision.
    expect(isPreselectedAbsence(candidate({ hasPresentOverride: true }))).toBe(false);
  });
});
