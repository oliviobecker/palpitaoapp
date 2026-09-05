import { describe, expect, it } from 'vitest';
import { RoundStatus } from '../../core/models/enums';
import { AbsenceCandidateRound, AbsenceReviewRound } from '../../core/models/models';
import {
  absenceReviewHintKey,
  absenceReviewToastKey,
  isPreselectedAbsence,
  toAbsenceReviewDecisions,
} from './admin-participants';

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

function reviewRound(partial: Partial<AbsenceReviewRound> = {}): AbsenceReviewRound {
  return {
    roundId: 'r1',
    number: 1,
    title: null,
    status: RoundStatus.Locked,
    matchCount: 10,
    predictionCount: 0,
    isAbsent: true,
    hasOverride: false,
    requiresRecalculation: false,
    absenceNumber: null,
    penaltyPoints: null,
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

describe('toAbsenceReviewDecisions', () => {
  it('gives every listed round an explicit decision, ticked meaning absent', () => {
    const rounds = [reviewRound({ roundId: 'r1' }), reviewRound({ roundId: 'r2' })];

    expect(toAbsenceReviewDecisions(rounds, ['r2'])).toEqual([
      { roundId: 'r1', isAbsent: false },
      { roundId: 'r2', isAbsent: true },
    ]);
  });

  it('ignores ticked ids that were not listed', () => {
    // Only rounds the admin actually saw may change; anything else is not a decision.
    expect(toAbsenceReviewDecisions([reviewRound({ roundId: 'r1' })], ['r1', 'ghost'])).toEqual([
      { roundId: 'r1', isAbsent: true },
    ]);
  });

  it('is empty when nothing was listed', () => {
    expect(toAbsenceReviewDecisions([], ['r1'])).toEqual([]);
  });
});

describe('absenceReviewHintKey', () => {
  it('quotes the recorded ordinal and penalty of a scored round', () => {
    expect(
      absenceReviewHintKey(
        reviewRound({ status: RoundStatus.Scored, requiresRecalculation: true, absenceNumber: 3 }),
      ),
    ).toBe('adminParticipants.reviewRoundScored');
  });

  it('still warns about the replay for a scored round with nothing on the ladder', () => {
    // E.g. a round before AbsenceFromRound: zeroed, but no Absence row.
    expect(
      absenceReviewHintKey(
        reviewRound({ status: RoundStatus.Scored, requiresRecalculation: true }),
      ),
    ).toBe('adminParticipants.reviewRoundScoredNoLadder');
  });

  it('flags an existing override on a locked round', () => {
    expect(absenceReviewHintKey(reviewRound({ hasOverride: true, isAbsent: false }))).toBe(
      'adminParticipants.reviewRoundOverride',
    );
  });

  it('tells when a plain locked round takes effect', () => {
    expect(absenceReviewHintKey(reviewRound())).toBe('adminParticipants.reviewRoundLocked');
  });
});

describe('absenceReviewToastKey', () => {
  it('reports no changes when nothing was stored', () => {
    expect(absenceReviewToastKey({ changedRounds: 0, recalculated: false })).toBe(
      'adminParticipants.reviewNoChanges',
    );
  });

  it('reports a plain review when only locked rounds changed', () => {
    expect(absenceReviewToastKey({ changedRounds: 2, recalculated: false })).toBe(
      'adminParticipants.reviewedMsg',
    );
  });

  it('reports the season replay when a scored round changed', () => {
    expect(absenceReviewToastKey({ changedRounds: 1, recalculated: true })).toBe(
      'adminParticipants.reviewedRecalcMsg',
    );
  });
});
