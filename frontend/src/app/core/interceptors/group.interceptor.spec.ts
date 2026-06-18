import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { groupInterceptor } from './group.interceptor';

/** Minimal fake request that records clone() calls. */
function fakeReq() {
  const cloned = {} as HttpRequest<unknown>;
  const req = {
    clone: vi.fn().mockReturnValue(cloned),
  } as unknown as HttpRequest<unknown>;
  return { req, cloned };
}

describe('groupInterceptor', () => {
  afterEach(() => localStorage.clear());

  it('adds the X-Group-Id header when a group is selected', () => {
    localStorage.setItem('palpitao.groupId', 'g-123');
    const { req, cloned } = fakeReq();
    const next = vi.fn().mockReturnValue('result') as unknown as HttpHandlerFn;

    const out = groupInterceptor(req, next);

    expect(req.clone).toHaveBeenCalledWith({ setHeaders: { 'X-Group-Id': 'g-123' } });
    expect(next).toHaveBeenCalledWith(cloned);
    expect(out).toBe('result');
  });

  it('passes the request through unchanged when no group is selected', () => {
    const { req } = fakeReq();
    const next = vi.fn().mockReturnValue('result') as unknown as HttpHandlerFn;

    groupInterceptor(req, next);

    expect(req.clone).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledWith(req);
  });
});
