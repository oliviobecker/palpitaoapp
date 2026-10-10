import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '@env/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/http-context';
import { Competition, MatchPhase } from '../models/enums';
import { AdminService } from './admin.service';

/**
 * The admin HTTP contract, call by call: verb, URL (with query), body, whether the error toast is
 * skipped, and the response type. The e2e mock answers any unmatched call with an empty 200, so a
 * wrong URL would pass e2e — this table is what proves a refactor kept every request identical.
 */
interface ContractCase {
  name: string;
  call: () => Observable<unknown>;
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  url: string;
  body?: unknown;
  skipToast?: boolean;
  responseType?: 'json' | 'blob';
}

const A = `${environment.apiBaseUrl}/admin`;
const admin = () => TestBed.inject(AdminService);

const cases: ContractCase[] = [
  // Flávio Rule overrides
  {
    name: 'getFlavioOverrides',
    call: () => admin().getFlavioOverrides('r1'),
    method: 'GET',
    url: `${A}/rounds/r1/flavio-overrides`,
  },
  {
    name: 'setFlavioOverride',
    call: () =>
      admin().setFlavioOverride('r1', { userId: 'u1', isExempt: true, justification: 'j' }),
    method: 'PUT',
    url: `${A}/rounds/r1/flavio-overrides`,
    body: { userId: 'u1', isExempt: true, justification: 'j' },
  },
  // Participants
  {
    name: 'listParticipants',
    call: () => admin().listParticipants(),
    method: 'GET',
    url: `${A}/users`,
  },
  {
    name: 'createParticipant',
    call: () => admin().createParticipant({ name: 'N', email: 'e@x.com', password: 'p' }),
    method: 'POST',
    url: `${A}/users`,
    body: { name: 'N', email: 'e@x.com', password: 'p' },
  },
  {
    name: 'updateParticipant',
    call: () => admin().updateParticipant('u1', { name: 'N', email: 'e@x.com' }),
    method: 'PUT',
    url: `${A}/users/u1`,
    body: { name: 'N', email: 'e@x.com' },
  },
  {
    name: 'activateParticipant',
    call: () => admin().activateParticipant('u1', ['r1']),
    method: 'POST',
    url: `${A}/users/u1/activate`,
    body: { absentRoundIds: ['r1'] },
  },
  {
    name: 'activateParticipant (no rounds)',
    call: () => admin().activateParticipant('u1'),
    method: 'POST',
    url: `${A}/users/u1/activate`,
    body: { absentRoundIds: [] },
  },
  {
    name: 'deactivateParticipant',
    call: () => admin().deactivateParticipant('u1'),
    method: 'POST',
    url: `${A}/users/u1/deactivate`,
    body: {},
  },
  {
    name: 'eliminateParticipant',
    call: () => admin().eliminateParticipant('u1', 'j'),
    method: 'POST',
    url: `${A}/users/u1/eliminate`,
    body: { justification: 'j' },
  },
  {
    name: 'reactivate',
    call: () => admin().reactivate('u1', 'j', ['r1']),
    method: 'POST',
    url: `${A}/users/u1/reactivate`,
    body: { justification: 'j', absentRoundIds: ['r1'] },
  },
  {
    name: 'getAbsenceCandidateRounds',
    call: () => admin().getAbsenceCandidateRounds('u1'),
    method: 'GET',
    url: `${A}/users/u1/absence-candidates`,
    skipToast: true,
  },
  {
    name: 'getUserAbsences',
    call: () => admin().getUserAbsences('u1'),
    method: 'GET',
    url: `${A}/users/u1/absences`,
  },
  {
    name: 'getAbsenceReviewRounds',
    call: () => admin().getAbsenceReviewRounds('u1'),
    method: 'GET',
    url: `${A}/users/u1/absence-review`,
  },
  {
    name: 'reviewAbsences',
    call: () => admin().reviewAbsences('u1', 'j', [{ roundId: 'r1', isAbsent: false }]),
    method: 'POST',
    url: `${A}/users/u1/absence-review`,
    body: { justification: 'j', rounds: [{ roundId: 'r1', isAbsent: false }] },
  },
  // Round absences
  {
    name: 'overrideAbsence',
    call: () => admin().overrideAbsence('r1', { userId: 'u1', isAbsent: true, justification: 'j' }),
    method: 'POST',
    url: `${A}/rounds/r1/absences/override`,
    body: { userId: 'u1', isAbsent: true, justification: 'j' },
  },
  // Manual predictions
  {
    name: 'saveManualPredictions',
    call: () =>
      admin().saveManualPredictions('r1', {
        userId: 'u1',
        predictions: [{ roundMatchId: 'm1', predictedHomeScore: 2, predictedAwayScore: 1 }],
        overwriteExisting: true,
      }),
    method: 'POST',
    url: `${A}/rounds/r1/predictions/manual`,
    body: {
      userId: 'u1',
      predictions: [{ roundMatchId: 'm1', predictedHomeScore: 2, predictedAwayScore: 1 }],
      overwriteExisting: true,
    },
  },
  {
    name: 'getParticipantPredictions',
    call: () => admin().getParticipantPredictions('r1', 'u1'),
    method: 'GET',
    url: `${A}/rounds/r1/predictions/participant/u1`,
  },
  {
    name: 'getPredictionCoverage',
    call: () => admin().getPredictionCoverage('r1'),
    method: 'GET',
    url: `${A}/rounds/r1/predictions/coverage`,
    skipToast: true,
  },
  // OCR imports
  {
    name: 'getOcrBatch',
    call: () => admin().getOcrBatch('b1'),
    method: 'GET',
    url: `${A}/ocr-imports/b1`,
  },
  {
    name: 'listOcrBatches',
    call: () => admin().listOcrBatches('r1'),
    method: 'GET',
    url: `${A}/rounds/r1/ocr-imports`,
  },
  {
    name: 'getOcrImage',
    call: () => admin().getOcrImage('b1'),
    method: 'GET',
    url: `${A}/ocr-imports/b1/image`,
    skipToast: true,
    responseType: 'blob',
  },
  {
    name: 'updateOcrCandidate',
    call: () => admin().updateOcrCandidate('b1', 'c1', { userId: 'u1', predictedHomeScore: 1 }),
    method: 'PUT',
    url: `${A}/ocr-imports/b1/candidates/c1`,
    body: { userId: 'u1', predictedHomeScore: 1 },
  },
  {
    name: 'deleteOcrCandidate',
    call: () => admin().deleteOcrCandidate('b1', 'c1'),
    method: 'DELETE',
    url: `${A}/ocr-imports/b1/candidates/c1`,
  },
  {
    name: 'confirmOcr',
    call: () => admin().confirmOcr('b1'),
    method: 'POST',
    url: `${A}/ocr-imports/b1/confirm`,
    body: {},
  },
  {
    name: 'cancelOcr',
    call: () => admin().cancelOcr('b1'),
    method: 'POST',
    url: `${A}/ocr-imports/b1/cancel`,
    body: {},
  },
  // OCR participant aliases
  {
    name: 'listOcrAliases',
    call: () => admin().listOcrAliases(),
    method: 'GET',
    url: `${A}/ocr-aliases`,
  },
  {
    name: 'createOcrAlias',
    call: () => admin().createOcrAlias('Tico', 'u1'),
    method: 'POST',
    url: `${A}/ocr-aliases`,
    body: { aliasRaw: 'Tico', userId: 'u1' },
  },
  {
    name: 'updateOcrAlias',
    call: () => admin().updateOcrAlias('a1', 'u1'),
    method: 'PUT',
    url: `${A}/ocr-aliases/a1`,
    body: { userId: 'u1' },
  },
  {
    name: 'deleteOcrAlias',
    call: () => admin().deleteOcrAlias('a1'),
    method: 'DELETE',
    url: `${A}/ocr-aliases/a1`,
  },
  // External fixtures
  {
    name: 'searchFixtures',
    call: () =>
      admin().searchFixtures({ startDate: '2026-08-01', endDate: '2026-08-07', roundId: 'r1' }),
    method: 'POST',
    url: `${A}/fixtures/search`,
    body: { startDate: '2026-08-01', endDate: '2026-08-07', roundId: 'r1' },
  },
  {
    name: 'searchFixtures (silent)',
    call: () =>
      admin().searchFixtures({ startDate: '2026-08-01', endDate: '2026-08-07' }, { silent: true }),
    method: 'POST',
    url: `${A}/fixtures/search`,
    body: { startDate: '2026-08-01', endDate: '2026-08-07' },
    skipToast: true,
  },
  {
    name: 'importFixtures',
    call: () =>
      admin().importFixtures('r1', {
        fixtures: [
          {
            externalId: 'x1',
            competition: Competition.PremierLeague,
            phase: MatchPhase.Regular,
            homeTeamName: 'Arsenal',
            awayTeamName: 'Chelsea',
            startsAt: '2026-08-01T15:00:00Z',
          },
        ],
      }),
    method: 'POST',
    url: `${A}/rounds/r1/matches/import`,
    body: {
      fixtures: [
        {
          externalId: 'x1',
          competition: Competition.PremierLeague,
          phase: MatchPhase.Regular,
          homeTeamName: 'Arsenal',
          awayTeamName: 'Chelsea',
          startsAt: '2026-08-01T15:00:00Z',
        },
      ],
    },
  },
  // Results, teams, scout
  {
    name: 'refreshResults',
    call: () => admin().refreshResults('r1'),
    method: 'POST',
    url: `${A}/rounds/r1/refresh-results`,
    body: {},
  },
  {
    name: 'updateTeamDivision',
    call: () => admin().updateTeamDivision('t1', Competition.Championship),
    method: 'PATCH',
    url: `${A}/teams/t1`,
    body: { division: Competition.Championship },
  },
  {
    name: 'syncTeamsPreview',
    call: () => admin().syncTeamsPreview(),
    method: 'POST',
    url: `${A}/teams/sync-preview`,
    body: {},
  },
  {
    name: 'syncTeamsApply',
    call: () => admin().syncTeamsApply(),
    method: 'POST',
    url: `${A}/teams/sync-apply`,
    body: {},
  },
  {
    name: 'getRoundScout',
    call: () => admin().getRoundScout('r1'),
    method: 'GET',
    url: `${A}/rounds/r1/scout`,
  },
  // Registration requests
  {
    name: 'listRegistrationRequests',
    call: () => admin().listRegistrationRequests(),
    method: 'GET',
    url: `${A}/registration-requests`,
  },
  {
    name: 'approveRegistration',
    call: () => admin().approveRegistration('u1'),
    method: 'POST',
    url: `${A}/registration-requests/u1/approve`,
    body: {},
  },
  {
    name: 'rejectRegistration',
    call: () => admin().rejectRegistration('u1', 'why'),
    method: 'POST',
    url: `${A}/registration-requests/u1/reject`,
    body: { reason: 'why' },
  },
  // Audit
  {
    name: 'getAuditLogs',
    call: () =>
      admin().getAuditLogs({
        userId: 'u1',
        entityName: 'Round',
        from: '2026-01-01',
        to: '2026-12-31',
      }),
    method: 'GET',
    url: `${A}/audit?userId=u1&entityName=Round&from=2026-01-01&to=2026-12-31`,
  },
  {
    name: 'getAuditLogs (no filter)',
    call: () => admin().getAuditLogs(),
    method: 'GET',
    url: `${A}/audit`,
  },
];

describe('admin HTTP contract', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it.each(cases)('$name', (c) => {
    c.call().subscribe();

    const req = http.expectOne((r) => r.urlWithParams === c.url);
    expect(req.request.method).toBe(c.method);
    if (c.body !== undefined) {
      expect(req.request.body).toEqual(c.body);
    }
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(c.skipToast ?? false);
    expect(req.request.responseType).toBe(c.responseType ?? 'json');
    req.flush(c.responseType === 'blob' ? new Blob() : null);
  });

  it.each([
    { silent: false, skipToast: false },
    { silent: true, skipToast: true },
  ])(
    'importImage sends the file and language as a form (silent: $silent)',
    ({ silent, skipToast }) => {
      const file = new File(['x'], 'shot.png', { type: 'image/png' });
      admin().importImage('r1', file, 'por', { silent }).subscribe();

      const req = http.expectOne(`${A}/rounds/r1/predictions/import-image`);
      expect(req.request.method).toBe('POST');
      const form = req.request.body as FormData;
      expect(form.get('file')).toBeInstanceOf(File);
      expect(form.get('language')).toBe('por');
      expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(skipToast);
      req.flush(null);
    },
  );
});
