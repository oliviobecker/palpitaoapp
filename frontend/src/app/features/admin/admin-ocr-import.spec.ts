import { describe, expect, it } from 'vitest';
import { validateOcrFile } from './admin-ocr-import';

const MB = 1024 * 1024;

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
