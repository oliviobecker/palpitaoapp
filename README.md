# Palpitão / FanPicks

> **FanPicks** is the English product name. **Palpitão** is the Portuguese product name.
> They are the same application — a generic **football prediction platform**.

A **multi-group** football prediction platform: each group is an independent pool, with its
own tournament type, administrators, participants, rounds, matches, predictions and standings.
The admin creates rounds with matches; participants predict the score until the first match
kicks off; the system scores them, applies absences and the Flávio Rule, and keeps the overall
standings — per group, with full isolation between groups.

There are two tournament types today (`TournamentType`): **Palpitão England** (Premier League,
FA Cup, Championship, League One) and **FIFA World Cup**. Groups have their own names —
_Palpitão England 2025/2026_, _Palpitão World Cup_, _World Cup 2026_, _Friends League_… — which
are **group/season names, not the app name**.

A monorepo with a **.NET 10 backend** (Web API + EF Core code-first + PostgreSQL) and an
**Angular 21 frontend** (mobile-first, Bootstrap 5).

---

## Product name and branding

The application is a football prediction platform.

- **English product name:** FanPicks
- **Portuguese product name:** Palpitão

Groups and tournaments can have custom names, such as:

- Palpitão England · Palpitão World Cup · Palpitão Brasileirão
- England Predictions · World Cup 2026 · Friends League · custom group names

Names like **"England 2025/2026"** are examples of **groups or seasons**, not the application
name. The product name is shown in the active language (FanPicks in `en-US`, Palpitão in
`pt-BR`) via the `app.name` translation key; the current group name is shown separately in the
header. The seeded default group is named _Palpitão England 2025/2026_ — that is a group/season
name, not the app's name.

---

## 1. Overview

- **Rounds** created manually by the admin, with the lifecycle `Draft → Published → Locked → Scored` (or `Cancelled`).
- **Predictions** of the score per match, editable while the round is open; the deadline is the first match kickoff.
- **Prediction mirror** released after the round is locked.
- **Scoring** by column/exact score, with **multipliers** by competition/phase/classic.
- **Absences** with progressive penalties and elimination on the 5th.
- **Flávio Rule**: from round 16 on, the leader gets a special deadline; if late, loses half of the round's points.
- **Overall standings**, ordered and recomputable idempotently.

## 2. Stack

| Layer | Technology |
|---|---|
| Backend | C# / .NET 10, ASP.NET Core Web API (controllers) |
| ORM / Database | EF Core 10 (code-first) + PostgreSQL 16 |
| Auth | JWT Bearer + BCrypt |
| Backend tests | xUnit + SQLite in-memory (317 tests) |
| Frontend | Angular 21 (standalone, signals), TypeScript |
| UI | Bootstrap 5 (mobile-first) |
| Frontend tests | Vitest (Angular 21 default runner) |

> **Why Bootstrap** (instead of Material/Tailwind): mobile-first by default, already integrated,
> ready-made components (navbar, cards, toasts, modals) and a lean bundle for a simple UI.

## 3. Folder structure

```
palpitaoapp/
├── backend/
│   ├── src/Palpitao.Api/
│   │   ├── Auth/            # JWT (settings, token service, claims)
│   │   ├── Common/          # business exceptions (BusinessRule/NotFound)
│   │   ├── Controllers/     # Auth, Rounds, Matches, Predictions, Seasons, Teams, Admin*
│   │   ├── Data/            # AppDbContext, SeedIds, design-time factory, Migrations
│   │   ├── DTOs/            # input/output contracts per area
│   │   ├── Entities/        # User, Season, Team, Round, RoundMatch, Prediction, ...
│   │   ├── Enums/           # Competition, MatchPhase, RoundStatus, ScoreCategory, ...
│   │   ├── Middlewares/     # global error handling (localized)
│   │   └── Services/        # Scoring, Rounds, Predictions, Absences, Flavio,
│   │                        #   Standings, Seasons, Users, Audit
│   └── tests/Palpitao.Api.Tests/
├── frontend/
│   └── src/app/
│       ├── core/            # auth, interceptors, models, notifications, services
│       ├── shared/          # components (badges, countdown, loading, empty, ...) + utils
│       ├── layout/          # responsive Shell (desktop topbar + mobile bottom nav)
│       └── features/        # auth(login), dashboard, rounds, standings, admin
├── docker-compose.yml       # PostgreSQL 16
├── .env.example             # database variables (docker-compose)
└── README.md
```

## 4. Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/) · [Node.js 20+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) (for PostgreSQL) **or** a local PostgreSQL
- EF Core CLI: `dotnet tool install --global dotnet-ef`

## 5. Start PostgreSQL

```bash
cp .env.example .env        # adjust user/password/port if you want
docker compose up -d        # PostgreSQL on localhost:5432
```

The default connection string (`backend/src/Palpitao.Api/appsettings.json`) already points to
`Host=localhost;Port=5432;Database=palpitao;Username=palpitao;Password=palpitao`.

## 6. Apply migrations

```bash
cd backend
dotnet ef database update --project src/Palpitao.Api
```

This creates all tables and the **initial seed** (7 Big Seven clubs + admin user).
For new migrations: `dotnet ef migrations add <Name> --project src/Palpitao.Api`.

## 7. Run the backend

```bash
cd backend
dotnet run --project src/Palpitao.Api
# API at https://localhost:7099 (and http://localhost:5146)
# Health: GET /api/health  and  GET /api/health/db
# OpenAPI (dev): GET /openapi/v1.json
```

## 8. Run the frontend

```bash
cd frontend
npm install
npm start                   # ng serve → http://localhost:4200
```

The development `apiBaseUrl` (`src/environments/environment.development.ts`) points to
`https://localhost:7099/api`; the backend CORS allows `http://localhost:4200`.

## 9. Users: initial admin, public sign-up and approval

### Initial admin (seed)

| Email | Password | Role |
|---|---|---|
| `admin@palpitao.local` | `Admin@123` | Admin |

Development only — change it in production.

### Public sign-up with admin approval

New participants can **sign up themselves** on the public **`/register`** screen ("Don't have an
account yet? Sign up" link on the login screen). Sign-up **does not grant access automatically**:

1. The user provides name, email, password and confirmation. Validations: name and email
   required, valid email, matching passwords and a strong password (at least 8 characters, with
   at least **one letter and one number**). Duplicate email is rejected.
2. `POST /api/auth/register` creates the user as `Role = Participant`,
   `Status = PendingApproval`, `IsActive = false`. The password is stored with a **BCrypt hash**;
   **no JWT** is issued. It returns only the success message.
3. The admin sees the requests in **/admin/registration-requests** ("Registration requests") and
   can **approve** or **reject** (with an optional reason).
4. Once approved, the user can log in normally as a participant.

The admin can also create participants directly in **/admin/participants** (already approved and
active).

### User status (`UserStatus`)

| Status | IsActive | Can log in? | Origin |
|---|---|---|---|
| `PendingApproval` | false | ❌ | public sign-up, awaiting approval |
| `Approved` | true | ✅ | approved by the admin / created by the admin |
| `Rejected` | false | ❌ | rejected by the admin (with `RejectionReason`) |
| `Inactive` | false | ❌ | participant deactivated by the admin |

**Login is allowed only when `Status = Approved` **and** `IsActive = true`.** In any other case
login is blocked with a friendly message (and the attempt is recorded in the `AuditLog` as
`LoginBlocked`). The `RegistrationSubmitted`, `RegistrationApproved` and `RegistrationRejected`
events are also audited.

### Messages in PT/EN

All messages (sign-up success and login blocks) are resolved by the backend according to the
`Accept-Language` header Angular sends (see §20). Examples:

| Situation | Portuguese | English |
|---|---|---|
| Sign-up submitted | "Cadastro enviado com sucesso. Aguarde a aprovação do administrador para acessar o sistema." | "Registration submitted successfully. Please wait for admin approval before accessing the system." |
| Login pending | "Seu cadastro ainda está pendente de aprovação." | "Your registration is still pending approval." |
| Login rejected | "Seu cadastro foi rejeitado. Entre em contato com o administrador." | "Your registration was rejected. Please contact the administrator." |
| Login inactive | "Sua conta está inativa. Entre em contato com o administrador." | "Your account is inactive. Please contact the administrator." |

### Test the flow manually

1. Go to `/register`, sign up a user (e.g. `john@x.com` / `Pass123`) → see the sign-up submitted message.
2. Try to log in as them at `/login` → login **blocked** with "still pending".
3. Logged in as admin, go to **/admin/registration-requests** → **approve** John.
4. Log in as John → now he **gets in** as a participant.
5. (Optional) Sign up another user and **reject** with a reason → login blocked with "was rejected".

## 10. Environment variables

**Backend** (`backend/.env.example`) — override `appsettings*.json`:

```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=palpitao;Username=palpitao;Password=palpitao
Jwt__Issuer=palpitao
Jwt__Audience=palpitao
Jwt__Key=<long random secret, >= 32 bytes>
```

**Root** (`.env.example`) — used by docker-compose: `POSTGRES_USER/PASSWORD/DB/PORT`.

**Frontend** (`frontend/.env.example`) — reference only; the effective value lives in
`src/environments/*.ts`.

## 11. Main endpoints

**Auth** · `POST /api/auth/login` · `POST /api/auth/register` (public sign-up pending approval)

**Rounds / Matches** (mutations: Admin)
- `GET /api/rounds` · `GET /api/rounds/{id}` · `POST /api/rounds` · `PUT /api/rounds/{id}` (round with `startDate`/`endDate`)
- `POST /api/rounds/{id}/publish|lock|cancel|score` · `GET /api/rounds/{id}/results`
- `POST /api/rounds/{roundId}/matches` · `PUT /api/matches/{id}` · `DELETE /api/matches/{id}`
- `POST /api/matches/{id}/result`

**External fixtures** (importing matches by period — see §24)
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
- `GET /api/admin/users/{id}/absences` · `GET /api/admin/rounds/{id}/absences` · `POST /api/admin/rounds/{id}/absences/override`
- `GET /api/admin/registration-requests` · `GET .../{userId}` · `POST .../{userId}/approve` · `POST .../{userId}/reject`
- `GET /api/admin/audit?userId&entityName&from&to`

## 12. Scoring rules

The **base** points of each prediction:

| Prediction outcome | Base points |
|---|---|
| Missed column and score | 0 |
| Got only the **column** right (winner/draw) | 1 |
| Exact score — **Traditional** | 3 |
| Exact score — **Medium** | 5 |
| Exact score — **Uncommon** | 7 |
| Exact score — **Extra-uncommon** | 10 |

Exact-score categories (symmetric — e.g. 1x0 ≡ 0x1):
- **Traditional**: 1x1, 1x0, 2x0, 2x1
- **Medium**: 0x0, 2x2, 3x1, 3x0
- **Uncommon**: 3x2, 4x0, 4x1, 3x3, 4x2
- **Extra-uncommon**: any other exact score (5x0, 4x3, …)

`Final match points = base points × multiplier`. A miss = 0 even with a multiplier.

## 13. Multipliers

| Competition / phase | Multiplier |
|---|---|
| Premier League — classic (two Big Seven) | 2 |
| Premier League — others | 1 |
| FA Cup — semifinal | 2 |
| FA Cup — final | 3 |
| FA Cup — Big Seven classic (regular phase) | 2 |
| Championship — playoff (semi/final) | 2 |
| Championship — others | 1 |
| League One — every match | 2 |

The phase prevails and **does not stack** (a Big Seven classic in the FA Cup final = 3, not 6).
**Big Seven**: Arsenal, Chelsea, Liverpool, Manchester City, Manchester United, Newcastle, Tottenham.
There is also a per-match **manual multiplier override** (requires a justification).

## 14. Absence rules

Absent = an active participant who did not submit **all** the round's required predictions (the
admin can apply an override). Penalty by season ordinal:

| Absence | Round points | Total penalty | Effect |
|---|---|---|---|
| 1st and 2nd | 0 | — | — |
| 3rd and 4th | 0 | −20 | — |
| 5th | 0 | — | **Eliminated** |

An eliminated participant no longer predicts, unless **manually reactivated** by the admin.

## 15. Flávio Rule

From **round 16** on, the standings leader (before the round) gets a special deadline:
- Reference = `MirrorPublishedAt` (or `PublishedAt`).
- Window = **24h**, or **12h** if the round was published less than 24h before the first match.
- The **general lock** (first match kickoff) always prevails.

If the leader completes the predictions **after** that deadline (but before the lock), they lose
**half** of the round's points (rounded down — 17 → 8). If they don't predict, they are treated as
a normal **absence**. A tie at the top ⇒ it applies to all tied leaders.

## 16. Overall standings

Shows position, name, total points, rounds played, absences, penalties and status
(active/eliminated). `Total = Σ(final points per round) − Σ(penalties)`. Ordering:
1. Total points (desc) → 2. Fewest absences → 3. Name (alphabetical).

**Recalculate season** (`POST /api/seasons/{id}/recalculate`) clears the calculations, resets
eliminations and re-scores the finished rounds in order — **idempotent**.

## 17. Implemented decisions and ambiguities

- **`ScoreCategory`** (ColumnOnly/Traditional/Medium/Uncommon/ExtraUncommon) reflects the
  exact-score difficulty taxonomy defined by the pool rules.
- **Mirror before the lock**: the API rejects with 422 (informative message); the frontend shows an
  empty state and no error toast.
- **Flávio Rule deadline milestone** = the leader's first complete submission (the latest
  `SubmittedAt` among their round predictions).
- **Tie at the top**: the Flávio Rule applies to all tied leaders.
- **Multiplier on the frontend** (before scoring): computed on the client mirroring the backend
  rule, by the Big Seven club names.
- **Active season**: only one at a time; the frontend derives `seasonId` from the rounds.
- **Dates/times** always in **UTC** in the database; displayed in the `pt-BR` timezone/locale.

## 18. Predictions entered by the admin (manual)

In a round (admin → **Round detail → Enter predictions**, route
`/admin/rounds/:id/manual-predictions`) the admin picks a participant, fills in the score of
**all** matches and saves. Endpoint: `POST /api/admin/rounds/{roundId}/predictions/manual`.

- By default it respects the round deadline (only `Published` and before the 1st match).
- **Override**: `allowAfterDeadline` + justification allows recording after the deadline or for an
  **eliminated** participant (recorded in the AuditLog).
- If predictions already exist, `overwriteExisting = true` is required (confirmation).
- Predictions are marked with **`Source = AdminManual`** and `CreatedBy/UpdatedBy`.

## 19. OCR import (Tesseract)

Admin → **Import from image** (`/admin/rounds/:id/import-predictions`): upload a screenshot
(PNG/JPG/JPEG/WEBP, ≤ 10 MB), the backend processes it with **Tesseract**, generates prediction
**candidates** (participant + match + score), the admin **reviews/corrects** them and only then
**confirms**. Confirmed predictions are marked with **`Source = AdminOcr`**.

Flow (never saves without review):
`upload → OCR → candidates → review → confirm`.
Endpoints: `POST /api/admin/rounds/{id}/predictions/import-image`,
`GET /api/admin/ocr-imports/{batchId}`,
`PUT /api/admin/ocr-imports/{batchId}/candidates/{candidateId}`,
`POST .../confirm`, `POST .../cancel`.

### Install/configure Tesseract

The `Tesseract` NuGet package ships the native libraries. The **language files**
(`traineddata`) are missing:

1. Download `por.traineddata` and `eng.traineddata` from
   https://github.com/tesseract-ocr/tessdata
2. Place them in **`backend/tessdata/`** (`backend/tessdata/por.traineddata`,
   `backend/tessdata/eng.traineddata`). See [backend/tessdata/README.md](backend/tessdata/README.md).
3. The path can be overridden via `Ocr:TessdataPath` (env `Ocr__TessdataPath`). On `dotnet run`
   (dev), point it to the absolute path of `backend/tessdata`.

### Limitations and why review is needed

OCR is heuristic: it depends on the image quality and the text format. The parser recognizes
common formats (`Arsenal 2x1 Chelsea`, `Maria: Arsenal 1-0 Chelsea`, `Pedro - Arsenal 2 Chelsea 1`)
and tries to match names/abbreviations (`Man City`, `Spurs`...), but **any uncertain item is marked
`NeedsReview` and is never saved without admin confirmation** — hence the mandatory review screen.

## 20. Languages (Portuguese / English)

- **Frontend**: [ngx-translate](https://github.com/ngx-translate/core) (language switching at
  **runtime**, no rebuild — that's why it's preferred over native Angular i18n). Detects
  `navigator.language` (`pt*` → `pt-BR`, otherwise `en-US`), persists it in `localStorage`, and
  there is a **PT/EN** selector in the top bar. Translations in
  [public/i18n/pt-BR.json](frontend/public/i18n/pt-BR.json) and
  [en-US.json](frontend/public/i18n/en-US.json).
- **Backend**: `LocalizationService` resolves the language by the **`Accept-Language`** header
  (`pt*` → Portuguese, otherwise English) and centralizes messages. Angular sends `Accept-Language`
  on every call (interceptor).

> **Note:** the i18n infrastructure is complete (detection, switching, interceptor, key messages
> and new translated screens). Extracting **all** strings from the legacy screens into the
> translation files is incremental work still in progress.

## 21. Monitoring with Sentry

The backend integrates the official `Sentry.AspNetCore` SDK to capture unhandled exceptions,
`Error`/`Critical` logs, breadcrumbs of important actions and safe request context. The application
keeps working normally when the DSN is empty.

Base configuration (`backend/src/Palpitao.Api/appsettings*.json`):

```json
"Sentry": {
  "Dsn": "",
  "Environment": "Development",
  "Release": "palpitao-backend@1.0.0",
  "TracesSampleRate": 0.0,
  "Debug": false,
  "SendDefaultPii": false,
  "MinimumBreadcrumbLevel": "Information",
  "MinimumEventLevel": "Error"
}
```

In production, prefer environment variables or host/IIS secrets:

```env
SENTRY_DSN=
SENTRY_ENVIRONMENT=Production
SENTRY_RELEASE=palpitao-backend@1.0.0
SENTRY_TRACES_SAMPLE_RATE=0.0
SENTRY_DEBUG=false
```

Never commit a real DSN or any secret. To disable event delivery, leave `SENTRY_DSN` empty. To
enable performance tracing, raise `SENTRY_TRACES_SAMPLE_RATE` gradually (e.g. `0.05` for 5% of
transactions); `0.0` keeps tracing off.

Data sent: exception, error level/log, route, HTTP method, traceId, environment, release,
breadcrumbs without sensitive payload and, when authenticated, the user id, the email already
present in the JWT and a role tag (`user.role`). The SDK runs with `SendDefaultPii=false`.

Data filtered before sending: `Authorization`, cookies, tokens/JWT, passwords, `PasswordHash`,
password confirmation, DSN, connection strings, uploaded files and the full OCR text. The global
middleware still returns friendly/localized messages for the API and includes `traceId` only on
500 errors.

Local Sentry test:

1. Set `SENTRY_DSN` in the development environment.
2. Run the backend in `Development`.
3. Authenticate as admin.
4. Call `GET /admin/sentry/test-error` (or `/api/admin/sentry/test-error` if the API is mounted as
   the `/api` application in IIS).

That endpoint returns 404 outside `Development` and requires admin.

## 22. How to run the tests

```bash
# Backend (317 tests — xUnit + SQLite in-memory)
cd backend && dotnet test

# Frontend (Vitest — 35 unit tests)
cd frontend && npm test          # or: npx ng test --watch=false

# Frontend e2e (Playwright — 25 tests; starts ng serve and mocks the API)
cd frontend && npm run e2e
```

## 23. Suggested next steps

- Refresh token and session expiration with renewal.
- Notifications (e.g. email/push) when a round opens or is about to close, or when a sign-up is
  approved/rejected.
- ESLint on the frontend and analyzers on the backend (CI already runs Prettier + build + tests).
- Pagination/indexes for AuditLog and Standings in long seasons.

## 24. Creating a round by period + importing matches

Instead of registering each match manually, the admin can **create the round by period** and
import the matches automatically from an external provider. The default provider is **OneFootball**
(free, no key, covers the **four** competitions — Premier League, Championship, League One and
FA Cup — with the **current season**). There are also `FixtureDownload`, `ApiFootball` and
`TheSportsDb` as alternatives. Switch via `Fixtures:Provider`.

> **Off-season:** in June/July OneFootball hasn't published the next season's matches yet, so the
> search comes back **empty** — that's expected (the Premier League starts in mid-August). Within
> the season (Aug–May) the matches of the four competitions appear normally.

### How it works

1. In **/admin/rounds/new**, the admin enters name/number, **start date** and **end date**.
2. Clicks **"Search matches"** → the backend queries the external provider
   (`POST /api/admin/fixtures/search`) and returns the matches in the period.
3. The matches appear **grouped by date**, with a **checkbox**, filters (competition and search by
   team), **select all**, **clear selection** and a **counter** of selected ones. Each card shows
   the competition, date/time, home × away, classic/suggested-multiplier badges and the source.
4. On save, the system creates the round and imports only the marked matches as `RoundMatch`
   (`POST /api/admin/rounds/{roundId}/matches/import`).

The same search/selection panel is available when **editing** an existing round
(**/admin/rounds/{id}/matches** → "Import matches by period"): already-added matches appear marked
as such, and "Add selected matches" imports directly into the round. The `FixtureSelection`
component is reused on both screens.

On that matches screen the search already runs **automatically on open** (pre-search): the period is
pre-filled with the round's window when defined, otherwise with the **next 8 days**, and the list is
ready for selection **if there are matches**. The pre-search is silent — if the external source is
unavailable, it shows no error toast and the admin proceeds with manual entry.

**When no match is found**, the new-round screen shows a notice and the button becomes
**"Create round and add matches manually"** — it creates the round and takes you straight to the
matches screen (add/edit manually). On the matches screen the manual add/edit form is always
available (draft/published rounds).

### Message for the group (copyable)

Once the round has matches, the **round detail** screen shows a **"Message for the group"** card
with a ready WhatsApp-style text — title, round number, **deadline (first match kickoff)** and the
matches grouped by competition with their multipliers/phases — plus a **Copy** button that works
even on mobile (Clipboard API with fallback). Just copy and paste it into the group.

**Flávio Rule in the message:** from **round 16** on, and only then, the message includes a line
with the current leader(s) and their **special deadline** (e.g. "Leader @Manoel Neto has until
23:59 on Friday (22/05/2026) to predict."). The backend computes this in `RoundDto.Flavio` (leaders
= top of the season standings; deadline = 24h, or 12h if the round was published less than 24h
before the first match, with the general lock prevailing). The line only appears when the round has
already been **published** (the deadline depends on the publish time) and there is a defined leader.

Non-existing teams are **created automatically** (with the correct `IsBigSevenClub` for the seven
giants); **duplicate** matches in the round are ignored; and a **second League One match** requires
a justification. Competitions outside the system's four are ignored.

> In June/July (off-season) the "next days" pre-search usually comes back **empty** — that's
> expected, since there are no published matches. Pick a period within the season (Aug–May).

### OneFootball provider (default, free, the four competitions)

`OneFootballFixtureProvider` queries OneFootball's public web-experience API
(`api.onefootball.com/web-experience/en/competition/{slug}/fixtures`) — one request per
competition, with a **timeout** and user-agent, no login/token. The response is a nested document of
`containers`; matches are extracted by walking the tree looking for objects with
`kickoff` + `homeTeam.name` + `awayTeam.name`, filtered by the period.

| Competition | OneFootball slug |
|---|---|
| Premier League | `premier-league-9` |
| Championship | `efl-championship-27` |
| League One | `efl-league-one-42` |
| FA Cup | `fa-cup-17` |

It is resilient: if **one** competition fails, the others continue; it only turns into the friendly
error **"Could not fetch matches from the external source right now."** when **all** fail — the
**manual flow** continues. The phase comes as `Regular` (adjust the knockout multiplier on the
matches screen for an FA Cup semi/final). ⚠️ It is an **undocumented** OneFootball API; if the
structure changes, switch to another provider in one config line.

**Configuration** (`appsettings.json` → `Fixtures`, or env `Fixtures__<Field>`):

```json
"Fixtures": {
  "Provider": "OneFootball",
  "OneFootballApiBaseUrl": "https://api.onefootball.com/web-experience/en/competition",
  "TimeoutSeconds": 15,
  "EnableExternalFixtureImport": true
}
```

### fixturedownload.com provider (alternative — only PL + Championship)

With `Fixtures:Provider=FixtureDownload`: a static JSON feed `…/feed/json/{epl|championship}-{year}`,
**free and no key**, full season, but **only** Premier League + Championship (League One and FA Cup
fall back to manual entry). More stable than OneFootball since it's a static feed.

### API-Football provider (alternative — covers all four, but paid for the current season)

With `Fixtures:Provider=ApiFootball`, it uses `ApiFootballFixtureProvider`
(`v3.football.api-sports.io`, header `x-apisports-key`, leagues 39/40/41/45). It is reliable, but
⚠️ **the Free plan only covers seasons 2022–2024** — querying the current season returns `"Free
plans do not have access to this season"` (handled as a friendly error). Live data needs a paid
plan. Configure `Fixtures:ApiKey` (preferably via env `Fixtures__ApiKey` / user-secrets).

### TheSportsDB provider (alternative)

With `Fixtures:Provider=TheSportsDb` (public key `3`). ⚠️ The free key returns only a **sample** (a
few matches per season/day), so most periods come back empty — useful only with a paid Patreon key.

### OneFootball provider (best-effort, alternative)

With `Fixtures:Provider=OneFootball`, it uses `OneFootballFixtureProvider`. OneFootball **does not
publish a stable public API**, so it is best-effort: a single GET with timeout/user-agent, no login
or bypass; if the source changes format, it fails with the same friendly message.

### Disable external import

`Fixtures:EnableExternalFixtureImport=false` (or env `Fixtures__EnableExternalFixtureImport=false`):
the search endpoint returns a friendly error and the admin uses only manual entry.

### Switching the provider in the future

The integration is isolated behind `IFixtureProvider` (no database access nor domain rules). To use
another source (SportMonks, etc.), just implement the interface and adjust the selection in
`Program.cs` — `FixtureImportService`, the controllers and the frontend don't change.

### Test with a mock

`FixtureImportServiceTests` uses a **`FakeFixtureProvider`** (period, normalization, team creation,
deduplication, League One limit, `FirstMatchStartsAt`, auditing). `OneFootballFixtureProviderTests`,
`FixtureDownloadFixtureProviderTests`, `TheSportsDbFixtureProviderTests` and
`ApiFootballFixtureProviderTests` use an **`HttpMessageHandler` stub** (slug/league, extraction of
nested match cards, period filter, resilience to partial failure, error handling) — **no test
touches the network**. On the frontend, `fixtures-import.e2e.ts` exercises search → multi-selection →
save/import with the API mocked.

## 25. Refreshing results + temporary standings

While a round is in progress the admin can **refresh the results** and everyone sees a **temporary
standings** (preview), without officially closing the round.

### How it works

1. In **/admin/rounds/{id}** (round detail), with the round `Published` or `Locked`, there is a
   **"Refresh results"** button.
2. It calls `POST /api/admin/rounds/{roundId}/refresh-results`, which: updates the available results
   (from the external provider, if active), stamps `Round.ResultsUpdatedAt` and does **not** change
   the round status. The response carries a summary (updated/finished/in-progress/not-started).
3. The **temporary standings** are at `GET /api/rounds/{roundId}/temporary-standings`
   (authenticated) and on the screen **/rounds/{id}/temporary-standings** (mobile cards, with the
   notice "points may change until the round ends"). Participants reach it via the link on the
   results screen.

### Temporary × official

| | Temporary | Official |
|---|---|---|
| When | round in progress (refresh) | only on **Compute scoring** |
| Round status | unchanged | becomes `Scored` |
| Matches counted | only those with a result (InProgress/Finished) | all (requires all finished) |
| Absence / elimination | does **not** apply | applies |
| Flávio Rule | does **not** apply | applies |
| Season standings | does **not** change | recalculated |

The temporary scoring uses the **same `ScoringService`** (categories + multipliers, including the
manual override). `projectedTotalPoints = current official scoring + the round's temporary points`.

### Persistence: on-demand calculation (Option A)

The temporary standings are **computed on demand** on the `GET` (there is no snapshot table). The
refresh only updates the results on the matches and stamps `ResultsUpdatedAt`; the `GET` recomputes
from that. Justified choice: the project is small/medium, it avoids an extra table and removes the
risk of stale snapshots; the results are already persisted on the `RoundMatch`.

### Results provider

The `IResultsProvider` abstraction (isolated, no domain rules). Default **`ManualResultsProvider`**
(`Enabled=false`): fetches nothing externally — results come from **manual entry** (the results
screen, which now marks the match as `Finished`), and the refresh only recomputes the temporary
standings. When no external provider is active, the endpoint responds with a clear message ("No
external results provider is active…") **without breaking**.

To integrate an external site/API, configure (`appsettings.json` → `ResultsProvider`, or env
`ResultsProvider__<Field>`):

```json
"ResultsProvider": { "Provider": "ConfiguredWebsite", "BaseUrl": "https://…", "Enabled": true, "TimeoutSeconds": 15 }
```

The `ConfiguredWebsiteResultsProvider` makes **one GET** (timeout + user-agent) expecting
`{ "results": [ { "homeTeam", "awayTeam", "homeScore", "awayScore", "status", "externalMatchId?", "url?" } ] }`
and matches by `externalMatchId` or team names; if the structure changes, it fails with
`results.fetchFailed` (friendly message) and the manual flow continues.

### Match status (`MatchStatus`)

`NotStarted` · `InProgress` · `Finished` · `Postponed` · `Cancelled`. Only `InProgress`/`Finished`
with a score enter the temporary standings; `Postponed`/`Cancelled` are ignored.

### How to test

- **Endpoint (manual):** publish a round with matches, register some results in
  **/admin/rounds/{id}/results** (they become `Finished`), go back to the detail and click
  **"Refresh results"** → see the summary. `GET /api/rounds/{id}/temporary-standings` shows the
  preview. The round status **stays** `Published`/`Locked`.
- **Frontend:** the button appears for the admin on the round detail; the temporary standings open at
  **/rounds/{id}/temporary-standings** (also linked on the participant's results screen).
- **Audit:** each refresh records `ResultsRefreshed` (or `ResultsRefreshFailed`) in the AuditLog with
  the provider and counts.

### Current limitations

- Without `ResultsProvider:Enabled=true` + `BaseUrl`, **there is no automatic fetch** — the results
  are manual. The `ConfiguredWebsiteResultsProvider` is a generic base (JSON contract above), not an
  integration with a specific site.
- The temporary standings include participants with at least one prediction in the round; whoever
  didn't predict appears only in the official scoring (with an absence), not in the preview.

## 26. Groups (multi-tenant)

The system is **multi-group**: each **group** is an independent pool (e.g. _Palpitão England
2025/2026_, _World Cup_, _Friends Group_) with its own administrators, participants, rounds,
matches, predictions, standings, access requests, OCR imports and auditing. **Data never crosses
between groups.**

### What groups are

- **`Group`** is the tenant: `Name`, `Slug` (unique), `Description?`, `OwnerUserId`, `IsActive`.
- **`GroupUser`** is the user↔group link, with `Role` (`GroupAdmin`/`Participant`) and `Status`
  (`PendingApproval`/`Approved`/`Rejected`/`Inactive`). Unique per `(GroupId, UserId)`.
- **`User`** is the **global** identity (email/password); the role is now **per group** (there is no
  more `SuperAdmin` at this stage).

### Create a group

1. On the login screen, click **Create a group** (`/create-group`).
2. Enter the group name + the administrator's name/email/password.
3. The backend creates the `User`, the `Group` (slug generated from the name) and a `GroupUser`
   `GroupAdmin/Approved`. Log in and you land on the group's `/admin`.

### Request access to a group

1. In **Sign up** (`/register`), pick the **desired group** (list of active groups via
   `GET /public/groups`).
2. The global account (approved) and a `GroupUser` **`PendingApproval`** in the group are created.
3. On login, while there is no approved group, it shows _"wait for the group administrator's
   approval"_.

### Per-group approval

- The admin sees only the requests **of their group** in `/admin/registration-requests` and
  approves/rejects only those (operations by `groupUserId`). Everything is audited with `GroupId`.

### Login and switching groups

- After authenticating, the frontend calls `GET /auth/my-groups`: **0** approved → pending message;
  **1** → enter directly; **several** → `/select-group` screen.
- The current group is kept in `localStorage` and shown in the header, with a **Switch group** button.

### How the frontend sends the group / how the backend validates it

- The `group.interceptor` injects the **`X-Group-Id`** header on every authenticated call.
- The `CurrentGroupService` (backend) reads that header, **validates** that the user has an
  `Approved` `GroupUser` in the group and exposes `GroupId`/`Role`. The `[RequireGroupAdmin]` /
  `[RequireGroupParticipant]` filters protect the controllers. A missing/invalid header or no access
  ⇒ **HTTP 403**. The frontend is **never** trusted alone — every endpoint revalidates the group.

### `GroupId` propagation (modeling decision)

To isolate with lean migrations, the `GroupId` column exists only on the **tenant roots** —
`Season`, `Round` (denormalized from Season), `Standing`, `RoundParticipantResult`, `AuditLog` and
`GroupUser`. The per-round entities (`Prediction`, `PredictionScore`, `Absence`, `AbsenceOverride`,
`RoundMatch`, `Ocr*`) **derive the group from the parent** (`Round`/`Season`), and every query
validates that the round/season belongs to the current group. The **roster** of a round (who scores/
is absent) comes from the **group membership** (`GroupQueries.ActiveParticipants`), not the global
role. **`Team`** remains a **global** catalog of real clubs.

### Migrating existing data

The `AddGroupsAndTenancy` migration creates the tables, gives a default `GroupId` to the current
rows pointing at a seeded **default group** — _Palpitão England 2025/2026_
(`palpitao-england-2025-2026`) — and links the seeded admin as `GroupAdmin/Approved` of that group.

### Multi-group security rules

- A group's admin **never** sees/manages another group's data.
- A participant only accesses a group where `GroupUser.Status = Approved`; pending/rejected/inactive
  is blocked.
- The header's `GroupId` is always revalidated in the backend; relevant actions go to the `AuditLog`
  with `GroupId`; Sentry receives the `group_id` tag.

### Current limitations

- `User.IsActive` / `User.IsEliminated` are still **global** (used by the calculation). For a user in
  more than one group, eliminating/deactivating reflects across all of them — moving those flags to
  `GroupUser` is left as future work.
- Creating a group via the public screen requires a **new** email (an existing user creating another
  group is left for later). `AdminSentryController` (diagnostics) still uses the global role.

### Test the isolation manually

1. Create 2 groups with different admins (`/create-group`).
2. In each, create a round/matches. Confirm one admin does **not** see the other's rounds.
3. Force another group's `X-Group-Id` on an authenticated call (DevTools) → response **403**.
4. Sign up a participant (`/register`) in one group and confirm they only appear in **that** group's
   requests.

## 27. Participant prediction visibility

By default, participants **cannot** see each other's predictions — only group admins can. A per-season
setting opens this up to participants, still respecting the post-lock timing of the mirror.

### The setting

`Season.AllowParticipantsToViewOthersPredictions` (boolean, **default `false`** for privacy). It lives
on the **season** (the certame instance), so the admin sets it when **creating or editing a season**
(admin → **Seasons**). Every change is written to the `AuditLog` (`SeasonUpdated`). A round resolves the
flag from its season, and the API exposes it on the round so the participant UI can show/hide the option.

### Who can see what

The prediction **mirror** (`GET /api/rounds/{roundId}/mirror`) is the single source — there is no
separate endpoint:

| | Setting `false` | Setting `true` |
|---|---|---|
| **Group admin** | sees the mirror (after the round is `Locked`/`Scored`) | same |
| **Participant** | **403 Forbidden** | sees the mirror once the round is `Locked`/`Scored` |

So a participant may view others' predictions only when **all** hold: approved member of the current
group **and** the round's season has `AllowParticipantsToViewOthersPredictions = true` **and** the round
is `Locked` or `Scored`. Before the lock predictions stay private for everyone (admins use the admin screens for the
in-progress round). The mirror returns matches, participants, each prediction with its submission time,
absent/eliminated/Flávio flags — and **no** sensitive data (no email, password hash, tokens or admin
justifications).

### Security

- The backend is the source of truth: a participant cannot bypass via a direct URL or API call —
  the API returns **403** (`mirror.notAllowed`) regardless of what the UI shows.
- The mirror is always scoped to the **current group** (`X-Group-Id`); a round from another group
  resolves to **404**. The frontend only **hides/shows** the option; it never grants access.

### How to test manually

- **As admin:** edit the season (admin → **Seasons**) and toggle the setting. With it **off**, open a
  `Locked` round's mirror — you (admin) still see it.
- **As participant, setting on:** wait for the round to be `Locked`/`Scored`, open **Rounds** → the
  **"View predictions"** button appears → see everyone's predictions.
- **As participant, setting off:** the **"View predictions"** button does not appear; hitting
  `/rounds/{id}/mirror` directly shows the "no permission" message, and the API returns **403**.

## 28. Prediction submission modes

Each **season** chooses **how predictions are entered**, via a per-season boolean
`Season.AllowParticipantsToSubmitPredictions` (kept as a simple boolean for consistency with the other
season flags). The admin picks it when **creating or editing a season** (admin → **Seasons**, "How will
predictions be submitted?"). Every change is audited (`SeasonUpdated`).

| Mode | Setting | Participant app | Admin |
|---|---|---|---|
| **Participants submit** (default) | `true` | normal predictions screen: submit/edit before the deadline | can also enter predictions manually / via OCR |
| **Admin only** | `false` | predictions screen is **read-only** with a notice; **no save** button; API returns **403** | enters all predictions manually or via OCR |

**Default is `true`** so existing seasons keep submitting in the app.

### Participant experience

- **Submit mode:** the score inputs and the **Save** button are shown; predictions can be edited until
  the round's first match.
- **Admin-only mode:** the screen shows _"In this season, predictions are entered by the
  administrator…"_, the form is read-only and there is **no Save button**.

### Admin experience

The round detail shows a badge — **"Predictions: participants in app"** or **"Predictions: admin
only"**. Regardless of the mode, the admin keeps the manual-entry, OCR import and OCR-review flows.
Editing the setting to admin-only when participant predictions already exist shows a warning; existing
predictions are **kept** — only new in-app submissions are blocked.

### Backend (source of truth)

The participant endpoint `POST|PUT /api/rounds/{roundId}/predictions` always writes
`Source = Participant`, so it is blocked entirely (**403** `prediction.appSubmitDisabled`) when the
season is admin-only — a participant can't bypass it via the API. The admin endpoints
(`/api/admin/rounds/{roundId}/predictions/manual`, `/predictions/import-image`,
`/api/admin/ocr-imports/{batchId}/confirm`) are **unaffected** and keep their own sources
(`AdminManual`, `AdminOcr`). So the backend never creates a `Participant`-sourced prediction in
admin-only mode.

### How to test manually

- **Create:** when creating/editing a season (admin → **Seasons**), pick "Only the administrator enters predictions".
- **Participant submits (submit mode):** open **Rounds → Predict**, enter scores, **Save**.
- **Admin-only:** as a participant, open a published round → read-only form + notice, no Save; calling
  `POST /api/rounds/{id}/predictions` directly returns **403**.
- **Admin manual:** **/admin/rounds/{id}/manual-predictions** works in either mode (source `AdminManual`).
- **OCR:** **/admin/rounds/{id}/import-predictions** works in either mode (source `AdminOcr`).

## 29. Security and secret configuration

This repository is public: **never** commit real secrets. The versioned files
(`appsettings*.json`, `.env.example`) carry only **placeholders**.

- Don't commit `.env` (already ignored); use the `*.env.example` files as a reference.
- The real connection string, `Jwt:Key`, `Sentry:Dsn` and `Fixtures:ApiKey` must come from
  **environment variables**, user-secrets (`dotnet user-secrets`) or GitHub Secrets — never from
  the code. In production the deploy workflow generates `appsettings.Production.json` from GitHub
  secrets.
- Don't commit `*.traineddata` (Tesseract models), uploads, local databases (`*.db`) or
  screenshots/images with real data.
- The seed (`admin@palpitao.local` / `Admin@123`) is **development only** — change it in any real
  environment.

Before going public (or when reviewing secrets), see
[PUBLIC_RELEASE_CHECKLIST.md](PUBLIC_RELEASE_CHECKLIST.md).

## 30. Continuous integration and deployment

GitHub Actions workflows live in `.github/workflows/`:

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | every pull request + push | Backend build + tests; frontend format check + build + unit + e2e; workflow lint (actionlint) |
| `deploy-staging.yml` | push to `main` (+ manual) | Auto-deploys to the **staging** environment |
| `release.yml` | push to `main` | **semantic-release**: tags + GitHub Release, then deploys **production** |
| `deploy-iis.yml` | called by `release.yml` (+ manual) | Builds + deploys to **production** (reusable) |

### Branch / PR flow

`main` is the single source of truth (trunk-based). Work on a feature branch, open a PR to `main`,
let CI go green, then merge. Merging into `main` auto-deploys to **staging** and, in parallel,
runs **semantic-release**: based on the [Conventional Commits](https://www.conventionalcommits.org/)
since the last release (`feat` → minor, `fix` → patch, `BREAKING CHANGE` → major) it decides the next
version, tags it and — if there's something to release — deploys that tag to **production**. So a
single merge can ship to staging and production; commits with no user-facing change (`chore`, `ci`,
`docs`, `refactor`, `test`) tag nothing and don't deploy to prod. To enforce the PR flow, enable a
branch ruleset on `main` (Settings → Branches): *Require a pull request before merging* and *Require
status checks to pass* (the `Backend`, `Frontend` and `Lint workflows (actionlint)` checks from
`ci.yml`).

### Staging deployment (`deploy-staging.yml`)

Runs on the **self-hosted** IIS runner. It restores, tests, publishes the backend, writes
`appsettings.Staging.json` from secrets, sets `ASPNETCORE_ENVIRONMENT=Staging` in `web.config`,
builds the frontend and copies both to the staging IIS site. Staging and production run on the
**same machine** as **separate IIS sites/app pools**, so they don't collide:

| | Production | Staging |
|---|---|---|
| Frontend IIS path | `C:\inetpub\palpitao` | `C:\inetpub\palpitao-staging` |
| Backend IIS path | `C:\inetpub\palpitao\api` | `C:\inetpub\palpitao-staging\api` |
| App pool | `palpitao-api` | `palpitao-staging-api` |

The staging paths/app pool are overridable repo **Variables** (`STAGING_FRONTEND_IIS_PATH`,
`STAGING_BACKEND_IIS_PATH`, `STAGING_BACKEND_APP_POOL`); the defaults above are used when unset.

**Required GitHub setup** before merging to `main`:

1. Create the `staging` **environment** (Settings → Environments).
2. Add its **secrets** — the **same names** as production (the environment scopes them):
   `BACKEND_CONNECTION_STRING`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_KEY` (and optional `SENTRY_DSN`).
   Point `BACKEND_CONNECTION_STRING` at the **staging database**.
3. On the server, create the staging **IIS site + `/api` application + app pool** at the paths above,
   pointing the connection string at a **separate staging database** (e.g. `palpitao_staging`) so it
   never touches production data.

> Secrets are **environment-scoped**, so `staging` and `production` each have their own
> `BACKEND_CONNECTION_STRING` / `JWT_*` — no prefix needed. Make sure they live under the matching
> environment, not loose at the repo level.

If a required secret is missing the job fails on purpose (at "Write backend staging settings")
without publishing.

### Releases & production deployment (`release.yml` + `deploy-iis.yml`)

Releases are **automatic** via [semantic-release](https://semantic-release.gitbook.io/). On each push
to `main` it analyses the Conventional Commits since the last `v*` tag, computes the next version,
creates the **git tag + GitHub Release** (the Release notes are your changelog), and then the
`deploy-production` job builds that tag and deploys it to the `production` environment. The app
**footer shows the version** — read at build time from the latest git tag (`git describe`), so prod
shows the released `v*` and staging shows the last release plus the short commit.

You don't bump versions by hand: just merge Conventional Commits and semantic-release does the rest.
It does **not** push a commit back to `main` (no bump commit), so it works with branch protection and
needs no PAT. `deploy-iis.yml` is a **reusable** workflow (`workflow_call`) invoked by `release.yml`;
you can also run it manually (**Actions → Build and deploy on IIS Production → Run workflow**,
optionally passing a `ref`) as a fallback. It targets the `production` environment and its secrets
(`BACKEND_CONNECTION_STRING`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_KEY`) and the production IIS paths.

> **Want a manual gate before prod instead of fully-automatic?** Swap semantic-release for
> [release-please](https://github.com/googleapis/release-please), which opens a "release PR" you merge
> when ready — that merge creates the tag and triggers the same production deploy.

## 31. License

Distributed under the **Apache 2.0** license — see [LICENSE](LICENSE). In short: free use,
modification and distribution (including commercial), keeping the copyright notice and the license,
with an explicit patent grant and no warranty.
