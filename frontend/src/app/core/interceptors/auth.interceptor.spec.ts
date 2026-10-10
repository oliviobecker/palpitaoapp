import { HttpContext, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { environment } from '@env/environment';
import { authInterceptor } from './auth.interceptor';
import { SKIP_TENANT_HEADERS } from './http-context';

function fakeReq(
  context: HttpContext = new HttpContext(),
  url = `${environment.apiBaseUrl}/rounds`,
) {
  const cloned = {} as HttpRequest<unknown>;
  const req = {
    url,
    context,
    clone: vi.fn().mockReturnValue(cloned),
  } as unknown as HttpRequest<unknown>;
  return { req, cloned };
}

const run = (fn: () => unknown) => TestBed.runInInjectionContext(fn);

describe('authInterceptor', () => {
  afterEach(() => localStorage.clear());

  it('attaches the bearer token when there is a session', () => {
    localStorage.setItem('palpitao.token', 'tok');
    const { req, cloned } = fakeReq();
    const next = vi.fn().mockReturnValue('result') as unknown as HttpHandlerFn;

    run(() => authInterceptor(req, next));

    expect(req.clone).toHaveBeenCalledWith({ setHeaders: { Authorization: 'Bearer tok' } });
    expect(next).toHaveBeenCalledWith(cloned);
  });

  it('sends no credentials on a request that opted out', () => {
    // A public link must behave the same for a signed-in reader as for a stranger.
    localStorage.setItem('palpitao.token', 'tok');
    const { req } = fakeReq(new HttpContext().set(SKIP_TENANT_HEADERS, true));
    const next = vi.fn().mockReturnValue('result') as unknown as HttpHandlerFn;

    run(() => authInterceptor(req, next));

    expect(req.clone).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledWith(req);
  });

  it('never sends the token to another host', () => {
    localStorage.setItem('palpitao.token', 'tok');
    const { req } = fakeReq(new HttpContext(), 'https://elsewhere.example/api/rounds');
    const next = vi.fn().mockReturnValue('result') as unknown as HttpHandlerFn;

    run(() => authInterceptor(req, next));

    expect(req.clone).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledWith(req);
  });
});
