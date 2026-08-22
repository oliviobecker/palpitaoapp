import { HttpContext, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { groupInterceptor } from './group.interceptor';
import { SKIP_TENANT_HEADERS } from './http-context';

/** Minimal fake request that records clone() calls. */
function fakeReq(context: HttpContext = new HttpContext()) {
  const cloned = {} as HttpRequest<unknown>;
  const req = {
    context,
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

  it('omits the group on a request that opted out, even with one selected', () => {
    // The public link resolves its own tenant from the key. Sending the group of whatever
    // tenant this browser happens to be signed into would filter that season away.
    localStorage.setItem('palpitao.groupId', 'g-123');
    const { req } = fakeReq(new HttpContext().set(SKIP_TENANT_HEADERS, true));
    const next = vi.fn().mockReturnValue('result') as unknown as HttpHandlerFn;

    groupInterceptor(req, next);

    expect(req.clone).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledWith(req);
  });
});
