import { RoundStatus } from '../../core/models/enums';
import { Round } from '../../core/models/models';

/**
 * Why the admin cannot enter predictions (manual entry or OCR import) for a round in this
 * status, as an i18n key — or null when entry is open. Mirrors the backend's
 * `AdminPredictionWindow`: the deadline does not close the round to the admin (the board often
 * imports the WhatsApp screenshots late), only the "Finalize round" click does.
 */
export function adminEntryBlockKey(status: RoundStatus | null | undefined): string | null {
  switch (status) {
    case RoundStatus.Scored:
      return 'adminEntry.blockedScored';
    case RoundStatus.Cancelled:
      return 'adminEntry.blockedCancelled';
    case RoundStatus.Draft:
      return 'adminEntry.blockedDraft';
    default:
      return null;
  }
}

export interface FlavioLateNotice {
  leaders: string;
  deadlineUtc: string;
}

/**
 * Predictions the admin enters are stamped with the time they are entered, so once the Flávio
 * deadline has passed a leader entered now counts as late. Non-null only then, so the screen can
 * point at the exemption panel for predictions that did arrive on time over WhatsApp.
 */
export function flavioLateNotice(
  round: Pick<Round, 'flavio'> | null | undefined,
  now = Date.now(),
): FlavioLateNotice | null {
  const flavio = round?.flavio;
  if (!flavio?.applies || !flavio.deadlineUtc || flavio.leaderNames.length === 0) {
    return null;
  }
  return now > new Date(flavio.deadlineUtc).getTime()
    ? { leaders: flavio.leaderNames.join(', '), deadlineUtc: flavio.deadlineUtc }
    : null;
}
