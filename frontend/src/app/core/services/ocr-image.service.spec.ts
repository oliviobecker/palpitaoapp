import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { OcrImageService } from './ocr-image.service';

const imageUrl = (batchId: string) =>
  `${environment.apiBaseUrl}/admin/ocr-imports/${batchId}/image`;
const IMAGE_URL = imageUrl('b1');

describe('OcrImageService', () => {
  let service: OcrImageService;
  let http: HttpTestingController;
  let revoked: string[];

  // jsdom implements neither of these, so they are patched onto the real URL object
  // (spreading URL would drop its constructor and the other statics Angular relies on).
  const originalCreate = URL.createObjectURL;
  const originalRevoke = URL.revokeObjectURL;

  beforeEach(() => {
    revoked = [];
    let n = 0;
    URL.createObjectURL = vi.fn(() => `blob:fake-${n++}`);
    URL.revokeObjectURL = vi.fn((url: string) => {
      revoked.push(url);
    });

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OcrImageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    URL.createObjectURL = originalCreate;
    URL.revokeObjectURL = originalRevoke;
  });

  /** Subscribes, flushes the pending request, and returns the emitted URL. */
  function loadAndFlush(batchId: string, url = IMAGE_URL): string | undefined {
    let emitted: string | undefined;
    service.load(batchId).subscribe((value) => (emitted = value));
    http.expectOne(url).flush(new Blob(['x'], { type: 'image/png' }));
    return emitted;
  }

  it('fetches the image as a blob and wraps it in an object URL', () => {
    let emitted: string | undefined;
    service.load('b1').subscribe((value) => (emitted = value));

    const req = http.expectOne(IMAGE_URL);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['x'], { type: 'image/png' }));

    expect(emitted).toBe('blob:fake-0');
    http.verify();
  });

  it('serves a second load from cache without another request', () => {
    expect(loadAndFlush('b1')).toBe('blob:fake-0');

    let second: string | undefined;
    service.load('b1').subscribe((value) => (second = value));

    expect(second).toBe('blob:fake-0');
    // No second GET: the object URL is memoised per batch.
    http.verify();
  });

  it('revokes the object URL on release', () => {
    loadAndFlush('b1');

    service.release('b1');

    expect(revoked).toEqual(['blob:fake-0']);
    expect(service.urlFor('b1')).toBeNull();
  });

  it('revokes every object URL on releaseAll', () => {
    loadAndFlush('b1');
    loadAndFlush('b2', imageUrl('b2'));

    service.releaseAll();

    expect(revoked).toHaveLength(2);
    expect(service.urlFor('b1')).toBeNull();
    expect(service.urlFor('b2')).toBeNull();
  });

  it('does not cache a failed load', () => {
    let failed = false;
    service.load('b1').subscribe({ error: () => (failed = true) });
    http.expectOne(IMAGE_URL).flush(null, { status: 404, statusText: 'Not Found' });

    expect(failed).toBe(true);
    expect(service.urlFor('b1')).toBeNull();
    // A retry issues a fresh request rather than replaying the cached error forever.
    service.load('b1').subscribe({ error: () => undefined });
    http.expectOne(IMAGE_URL).flush(null, { status: 404, statusText: 'Not Found' });
    http.verify();
  });
});
