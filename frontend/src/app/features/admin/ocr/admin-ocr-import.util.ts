import { OcrCandidate } from '@core/models';

const MAX_FILE_MB = 10;
const ALLOWED_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp'];

/**
 * The participant every candidate already points at, or null when they disagree (or none is
 * resolved). Drives the batch-wide selector: one screenshot is one person's predictions, so when
 * OCR misreads the name the admin fixes it once instead of on all twelve cards.
 */
export function commonParticipantId(candidates: readonly OcrCandidate[]): string | null {
  if (candidates.length === 0) {
    return null;
  }
  const first = candidates[0].userId ?? null;
  return first !== null && candidates.every((c) => (c.userId ?? null) === first) ? first : null;
}

/** Client-side mirror of the backend file rules, so a bad file fails before the upload. */
export function validateOcrFile(name: string, size: number): 'invalidFormat' | 'tooLarge' | null {
  const lower = name.toLowerCase();
  if (!ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext))) {
    return 'invalidFormat';
  }
  if (size > MAX_FILE_MB * 1024 * 1024) {
    return 'tooLarge';
  }
  return null;
}
