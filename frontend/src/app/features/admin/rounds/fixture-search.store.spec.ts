import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '@env/environment';
import { SKIP_ERROR_TOAST } from '@core/interceptors/http-context';
import { FixtureSearchStore, isRangeInvalid } from './fixture-search.store';

const SEARCH_URL = `${environment.apiBaseUrl}/admin/fixtures/search`;
const RANGE = { startDate: '2026-08-01', endDate: '2026-08-07' };
const FIXTURE = { externalId: 'x1', homeTeamName: 'Arsenal', awayTeamName: 'Chelsea' };

describe('FixtureSearchStore', () => {
  let store: FixtureSearchStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FixtureSearchStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(FixtureSearchStore);
    http = TestBed.inject(HttpTestingController);
  });

  it('searches whole days, scoped as asked, and shows what it found', () => {
    store.search(RANGE, { roundId: 'r1' });
    expect(store.searching()).toBe(true);

    const req = http.expectOne(SEARCH_URL);
    expect(req.request.body).toEqual({
      startDate: '2026-08-01T00:00:00',
      endDate: '2026-08-07T23:59:59',
      roundId: 'r1',
    });
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(false);
    req.flush({ fixtures: [FIXTURE], source: 'OneFootball' });

    expect(store.fixtures()).toEqual([FIXTURE]);
    expect(store.source()).toBe('OneFootball');
    expect([store.searched(), store.searching()]).toEqual([true, false]);
  });

  it('keeps a silent search with nothing found out of sight', () => {
    store.search(RANGE, { seasonId: 's1' }, { silent: true });

    const req = http.expectOne(SEARCH_URL);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush({ fixtures: [], source: 'OneFootball' });

    expect([store.searched(), store.searching()]).toEqual([false, false]);
  });

  it('flags a failed search so the page can offer the manual route', () => {
    store.search(RANGE, { roundId: 'r1' });
    http.expectOne(SEARCH_URL).flush(null, { status: 422, statusText: 'Unprocessable' });

    expect([store.searchError(), store.searching()]).toEqual([true, false]);
  });

  it('calls a backwards range invalid, and only once both dates are set', () => {
    expect(isRangeInvalid('2026-08-07', '2026-08-01')).toBe(true);
    expect(isRangeInvalid('2026-08-01', '2026-08-07')).toBe(false);
    expect(isRangeInvalid('2026-08-07', '')).toBe(false);
  });
});
