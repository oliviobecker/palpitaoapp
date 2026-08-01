import { MyPredictions, Round, RoundMatch, RoundSummary } from '../../core/models/models';

/** Minutes before the first kickoff at which predictions close (mirrors the backend). */
export const DEADLINE_LEAD_MINUTES = 1;

const LEAD_MS = DEADLINE_LEAD_MINUTES * 60_000;

type WithDeadline = Pick<RoundSummary, 'firstMatchStartsAt' | 'predictionDeadlineUtc'> & {
  matches?: RoundMatch[];
};

/**
 * The round's prediction deadline as an epoch, or null when it cannot be known yet.
 *
 * Prefers the value the API computes. Falls back to the first kickoff minus the lead
 * time, and finally to the earliest match of a *draft* round — drafts have no
 * `firstMatchStartsAt` yet (it is set at publication), but the admin still previews
 * their group message.
 */
export function predictionDeadline(round: WithDeadline | null | undefined): number | null {
  if (!round) {
    return null;
  }

  if (round.predictionDeadlineUtc) {
    return new Date(round.predictionDeadlineUtc).getTime();
  }

  if (round.firstMatchStartsAt) {
    return new Date(round.firstMatchStartsAt).getTime() - LEAD_MS;
  }

  const kickoffs = (round.matches ?? []).map((m) => new Date(m.startsAt).getTime());
  return kickoffs.length > 0 ? Math.min(...kickoffs) - LEAD_MS : null;
}

/** Same as {@link predictionDeadline}, as an ISO string for `[target]` inputs and date pipes. */
export function predictionDeadlineIso(round: WithDeadline | null | undefined): string | null {
  const ms = predictionDeadline(round);
  return ms === null ? null : new Date(ms).toISOString();
}

/** True once the round's predictions are closed. Pass `now` to make it reactive. */
export function deadlinePassed(round: WithDeadline | null | undefined, now = Date.now()): boolean {
  const ms = predictionDeadline(round);
  return ms !== null && now >= ms;
}

/** Narrowing helper so `Round`/`RoundSummary`/`MyPredictions` all fit the input type. */
export type DeadlineSource = Round | RoundSummary | MyPredictions | WithDeadline;
