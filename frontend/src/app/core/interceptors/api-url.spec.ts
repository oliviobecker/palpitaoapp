import { describe, expect, it } from 'vitest';
import { isApiRequest } from './api-url';

const APP = 'https://palpitao.example';

describe('isApiRequest', () => {
  describe('deployed: the API is a path on the app origin', () => {
    const api = (url: string) => isApiRequest(url, '/api', APP);

    it('accepts the API paths, relative or absolute', () => {
      expect(api('/api/rounds/r1')).toBe(true);
      expect(api('/api')).toBe(true);
      expect(api(`${APP}/api/auth/refresh`)).toBe(true);
    });

    it('rejects other paths and other hosts', () => {
      expect(api('/i18n/en-US.json')).toBe(false);
      expect(api('/apix/rounds')).toBe(false);
      expect(api('https://elsewhere.example/api/rounds')).toBe(false);
    });
  });

  describe('development: the API is another origin', () => {
    const api = (url: string) =>
      isApiRequest(url, 'https://localhost:7099', 'http://localhost:4200');

    it('accepts any path on the API origin', () => {
      expect(api('https://localhost:7099/rounds/r1')).toBe(true);
    });

    it('rejects the dev server itself and any other host', () => {
      expect(api('/i18n/en-US.json')).toBe(false);
      expect(api('http://localhost:7099/rounds')).toBe(false); // other scheme, other origin
      expect(api('https://cdn.example/crest.png')).toBe(false);
    });
  });
});
