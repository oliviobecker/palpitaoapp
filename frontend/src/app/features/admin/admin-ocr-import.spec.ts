import { describe, expect, it } from 'vitest';
import { OcrCandidate } from '../../core/models/models';
import { commonParticipantId, validateOcrFile } from './admin-ocr-import';

const MB = 1024 * 1024;

function candidate(userId: string | null): OcrCandidate {
  return {
    id: crypto.randomUUID(),
    userId,
    participantNameRaw: 'PL',
    roundMatchId: null,
    matchTextRaw: 'Wolves 2x0 Blackburn',
    predictedHomeScore: 2,
    predictedAwayScore: 0,
    confidence: 0.5,
    needsReview: true,
    reviewNotes: null,
  };
}

describe('commonParticipantId', () => {
  it('reports the participant when every candidate already agrees', () => {
    expect(commonParticipantId([candidate('u1'), candidate('u1')])).toBe('u1');
  });

  it('reports none when the candidates disagree', () => {
    expect(commonParticipantId([candidate('u1'), candidate('u2')])).toBeNull();
  });

  it('reports none when a candidate is still unresolved', () => {
    // The common case this feature exists for: OCR misread the name, so nothing is filed.
    expect(commonParticipantId([candidate('u1'), candidate(null)])).toBeNull();
    expect(commonParticipantId([candidate(null), candidate(null)])).toBeNull();
    expect(commonParticipantId([])).toBeNull();
  });
});

describe('validateOcrFile', () => {
  it('accepts the formats the backend accepts', () => {
    for (const name of ['foto.png', 'foto.jpg', 'foto.jpeg', 'foto.webp', 'FOTO.PNG']) {
      expect(validateOcrFile(name, MB)).toBeNull();
    }
  });

  it('rejects other formats before uploading', () => {
    for (const name of ['palpites.pdf', 'palpites.heic', 'palpites.txt', 'palpites']) {
      expect(validateOcrFile(name, MB)).toBe('invalidFormat');
    }
  });

  it('rejects files above the 10 MB backend limit', () => {
    expect(validateOcrFile('grande.png', 10 * MB + 1)).toBe('tooLarge');
    expect(validateOcrFile('ok.png', 10 * MB)).toBeNull();
  });
});
