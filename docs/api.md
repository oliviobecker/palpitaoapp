# HTTP API

The main endpoints, grouped by area. In deployed environments the API is mounted under `/api`
(an IIS sub-application); a local `dotnet run` serves the same routes from the root.

**Browse it:** in Development the backend serves the **Scalar** reference at
`https://localhost:7099/scalar` (try requests there: sign in with `POST /Auth/login`, paste the
token into the bearer field, and set `X-Group-Id`), and the raw OpenAPI document at
`/openapi/v1.json`. Both follow `OpenApi:Enabled`, which only appsettings.Development.json turns on.

**Errors** are RFC 7807 problems (`application/problem+json`) localized by `Accept-Language`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The e-mail is required.",
  "message": "The e-mail is required.",
  "traceId": "0HNP6VDAKSFFA:00000001"
}
```

`message` repeats `detail` for the SPA; `traceId` matches the server logs and Sentry
([ADR 0007](adr/0007-problem-details-with-message.md)).

**Auth** · `POST /api/auth/login` (returns access + refresh token) · `POST /api/auth/refresh` (rotate) · `POST /api/auth/logout` (revoke) · `POST /api/auth/register` (public sign-up, pending group approval) · `POST /api/auth/create-group` (public) · `GET /api/auth/my-groups` · `GET /api/auth/my-groups/pending` (pending/rejected/deactivated, for the `/pending` screen)

**Rounds / Matches** (mutations: Admin)
- `GET /api/rounds` · `GET /api/rounds/{id}` · `POST /api/rounds` · `PUT /api/rounds/{id}` (round with `startDate`/`endDate`; `joinPreviousWeek: true` on create plays it as the next part of the previous round — [Absences](domain-rules.md#absences))
- `POST /api/rounds/{id}/publish|lock|cancel|score|reopen|unlock|restore` · `GET /api/rounds/{id}/results`
- `DELETE /api/rounds/{id}` (Draft or Cancelled only; closes the numbering gap — [Absences](domain-rules.md#absences))
- `POST /api/rounds/{id}/join-previous-week` · `POST /api/rounds/{id}/leave-week` (rounds played in parts, "10.1"/"10.2" — [Absences](domain-rules.md#absences))
- `POST /api/rounds/{roundId}/matches` · `PUT /api/matches/{id}` · `DELETE /api/matches/{id}`
- `POST /api/matches/{id}/result`

**External fixtures** (importing matches by period — see [Creating a round by period and importing matches](features/fixtures-and-results.md#creating-a-round-by-period-and-importing-matches))
- `POST /api/admin/fixtures/search` (search matches in the period via provider)
- `POST /api/admin/rounds/{roundId}/matches/import` (import the selected matches)

**Predictions / Mirror**
- `GET /api/rounds/{roundId}/predictions/me` · `POST|PUT /api/rounds/{roundId}/predictions`
- `GET /api/rounds/{roundId}/mirror`

**Seasons / Standings**
- `GET /api/seasons` · `GET /api/seasons/active` · `POST /api/seasons` · `PUT /api/seasons/{id}` · `POST /api/seasons/{id}/activate`
- `GET /api/seasons/{id}/standings` · `POST /api/seasons/{id}/recalculate`

**Teams** · `GET /api/teams`

**Admin**
- `GET/POST /api/admin/users` · `PUT /api/admin/users/{id}` · `POST .../activate|deactivate|eliminate|reactivate`
- `GET /api/admin/users/{id}/absences` · `GET /api/admin/users/{id}/absence-candidates` · `GET|POST /api/admin/users/{id}/absence-review` · `GET /api/admin/rounds/{id}/absences` · `POST /api/admin/rounds/{id}/absences/override`
- `GET /api/admin/registration-requests` · `GET .../{userId}` · `POST .../{userId}/approve` · `POST .../{userId}/reject`
- `GET /api/admin/audit?userId&entityName&from&to`
