import {
  AbsenceCandidateRound,
  AbsenceReviewDecision,
  AbsenceReviewResult,
  AbsenceReviewRound,
} from '@core/models';

/**
 * Whether a candidate round starts out ticked. A locked round's absence lands on its own at
 * scoring time, so it is the safe default; a scored round needs a deliberate re-score, and a
 * round the participant was excused from needs a deliberate reversal.
 */
export function isPreselectedAbsence(round: AbsenceCandidateRound): boolean {
  return !round.requiresRescore && !round.hasPresentOverride;
}

/**
 * Every listed round gets an explicit decision — ticked means "counts as absent" — so a round
 * that closed between listing and confirming is never flipped by omission. Ticked ids that are
 * not in the list are ignored.
 */
export function toAbsenceReviewDecisions(
  rounds: AbsenceReviewRound[],
  checkedIds: string[],
): AbsenceReviewDecision[] {
  const checked = new Set(checkedIds);
  return rounds.map((r) => ({ roundId: r.roundId, isAbsent: checked.has(r.roundId) }));
}

/**
 * Secondary line under a reviewed round. A scored round warns that changing it replays the
 * season (quoting the recorded ordinal and penalty when there is one); otherwise an override,
 * then a locked round, get a note on when the decision takes effect.
 */
export function absenceReviewHintKey(round: AbsenceReviewRound): string {
  if (round.requiresRecalculation) {
    return round.absenceNumber != null
      ? 'adminParticipants.reviewRoundScored'
      : 'adminParticipants.reviewRoundScoredNoLadder';
  }
  if (round.hasOverride) {
    return 'adminParticipants.reviewRoundOverride';
  }
  return 'adminParticipants.reviewRoundLocked';
}

/** Which toast a finished review deserves: nothing changed, stored, or stored + season replayed. */
export function absenceReviewToastKey(result: AbsenceReviewResult): string {
  if (result.changedRounds === 0) {
    return 'adminParticipants.reviewNoChanges';
  }
  return result.recalculated
    ? 'adminParticipants.reviewedRecalcMsg'
    : 'adminParticipants.reviewedMsg';
}
